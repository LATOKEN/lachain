using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Google.Protobuf;
using Org.BouncyCastle.Security;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Blockchain.Pool;
using Phorkus.Core.Config;
using Phorkus.Core.Cryptography;
using Phorkus.Core.Logging;
using Phorkus.Core.Messaging;
using Phorkus.Core.Network;
using Phorkus.Proto;
using Phorkus.Core.Utils;

namespace Phorkus.Core.Consensus
{
    using Timer = System.Timers.Timer;

    public class ConsensusManager : IConsensusManager, IDisposable
    {
        private readonly IBlockManager _blockManager;
        private readonly ITransactionManager _transactionManager;
        private readonly IBlockchainContext _blockchainContext;
        private readonly ITransactionPool _transactionPool;
        private readonly IBroadcaster _broadcaster;
        private readonly ILogger<ConsensusManager> _logger;
        private readonly ITransactionFactory _transactionFactory;
        private readonly ICrypto _crypto;
        private readonly ConsensusContext _context;
        private readonly KeyPair _keyPair;
        private readonly IBlockchainSynchronizer _blockchainSynchronizer;

        private readonly object _quorumSignaturesAcquired = new object();
        private readonly object _prepareRequestReceived = new object();
        private readonly object _changeViewApproved = new object();
        private readonly object _timeToProduceBlock = new object();
        private Timer _timer;
        private bool _stopped;
        private readonly SecureRandom _random;

        private readonly TimeSpan _timePerBlock = TimeSpan.FromSeconds(15);

        public ConsensusManager(
            IBlockManager blockManager,
            ITransactionManager transactionManager,
            IBlockchainContext blockchainContext,
            ITransactionPool transactionPool,
            IBroadcaster broadcaster,
            ILogger<ConsensusManager> logger,
            IConfigManager configManager,
            ITransactionFactory transactionFactory,
            ICrypto crypto,
            IBlockchainSynchronizer blockchainSynchronizer)
        {
            var config = configManager.GetConfig<ConsensusConfig>("consensus");
            _blockManager = blockManager ?? throw new ArgumentNullException(nameof(blockManager));
            _transactionManager = transactionManager ?? throw new ArgumentNullException(nameof(transactionManager));
            _transactionPool = transactionPool ?? throw new ArgumentNullException(nameof(transactionPool));
            _transactionFactory = transactionFactory ?? throw new ArgumentNullException(nameof(transactionFactory));
            _blockchainSynchronizer =
                blockchainSynchronizer ?? throw new ArgumentNullException(nameof(blockchainSynchronizer));
            _blockchainContext = blockchainContext ?? throw new ArgumentNullException(nameof(blockchainContext));
            _broadcaster = broadcaster ?? throw new ArgumentNullException(nameof(broadcaster));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _crypto = crypto ?? throw new ArgumentNullException(nameof(crypto));
            
            _keyPair = new KeyPair(config.PrivateKey.HexToBytes().ToPrivateKey(), crypto);
            _context = new ConsensusContext(_keyPair,
                config.ValidatorsKeys.Select(key => key.HexToBytes().ToPublicKey()).ToList());
            _random = new SecureRandom();
            _stopped = true;
        }

        public void Stop()
        {
            _stopped = true;
        }

        private void _TaskWorker()
        {
            InitializeConsensus(0);
            if (_context.MyIndex < 0)
            {
                _logger.LogWarning("Halting consensus process: we are not in validator list");
                return;
            }
            
            _stopped = false;

            while (!_stopped)
            {
                // If were are waiting for view change, just wait
                if (_context.State.HasFlag(ConsensusState.ViewChanging))
                {
                    lock (_changeViewApproved)
                    {
                        byte viewNumber;
                        while (!CanChangeView(out viewNumber))
                        {
                            // TODO: manage timeouts
                            var timeToWait = TimeUtils.Multiply(_timePerBlock, 1 + _context.MyState.ExpectedViewNumber);
                            if (!Monitor.Wait(_changeViewApproved, timeToWait))
                            {
                                RequestChangeView();
                            }
                        }

                        InitializeConsensus(viewNumber);
                        continue;
                    }
                }

                if (_context.Role.HasFlag(ConsensusState.Primary))
                {
                    // if we are primary, wait until block must be produced
                    lock (_timeToProduceBlock)
                    {
                        if (DateTime.UtcNow - _context.LastBlockRecieved < _timePerBlock)
                            Monitor.Wait(_timeToProduceBlock);
                    }

                    // TODO: produce block
                    var blockBuilder = new BlockBuilder(_transactionPool, _blockchainContext.CurrentBlockHeader.Hash,
                        _blockchainContext.CurrentBlockHeader.Header.Index);
                    var address = _crypto.ComputeAddress(_context.KeyPair.PublicKey.Buffer.ToByteArray());
                    var from = address.ToUInt160();
                    var minerTx = _transactionFactory.MinerTransaction(from);
                    var signed = _transactionManager.Sign(minerTx, _keyPair);
                    var minerError = _transactionManager.Persist(signed);
                    if (minerError != OperatingError.Ok)
                    {
                        _logger.LogError(
                            $"Unable to persis miner transaction (it is very bad), cuz error {minerError}");
                        RequestChangeView();
                        continue;
                    }

                    var blockWithTransactions = blockBuilder.Build(signed, (ulong) _random.Next());
                    _logger.LogInformation($"Produced block with hash {blockWithTransactions.Block.Hash}");
                    _context.UpdateCurrentProposal(blockWithTransactions);
                    _context.CurrentProposal = new ConsensusProposal
                    {
                        TransactionHashes = blockWithTransactions.Transactions.Select(tx => tx.Hash).ToArray(),
                        Transactions = blockWithTransactions.Transactions.ToDictionary(tx => tx.Hash),
                        Timestamp = blockWithTransactions.Block.Header.Timestamp,
                        Nonce = blockWithTransactions.Block.Header.Nonce
                    };
                    _context.State |= ConsensusState.RequestSent;

                    if (!_context.State.HasFlag(ConsensusState.SignatureSent))
                    {
                        var signature = _blockManager.Sign(blockWithTransactions.Block.Header, _context.KeyPair);
                        OnSignatureAcquired(_context.MyIndex, signature);
                    }

                    SignAndBroadcast(
                        _context.MakePrepareRequest(blockWithTransactions, _context.MyState.BlockSignature));
                    _logger.LogInformation("Sent prepare request");
                }
                else
                {
                    // if we are backup, wait unitl someone sends prepare, or change view
                    lock (_prepareRequestReceived)
                    {
                        // TODO: manage timeouts
                        var timeToWait = TimeUtils.Multiply(_timePerBlock, 1 + _context.MyState.ExpectedViewNumber);
                        if (!Monitor.Wait(_prepareRequestReceived, timeToWait))
                        {
                            RequestChangeView();
                            continue;
                        }
                    }
                }

                // Regardless of our role, here we must collect transactions, signatures and assemble block
                var txsGot =
                    _blockchainSynchronizer.WaitForTransactions(_context.CurrentProposal.TransactionHashes,
                        _timePerBlock);
                if (txsGot != _context.CurrentProposal.TransactionHashes.Length)
                {
                    _logger.LogWarning(
                        $"Cannot retrieve all transactions in time, got only {txsGot} of {_context.CurrentProposal.TransactionHashes.Length}, aborting");
                    RequestChangeView();
                    continue;
                }

                // When all transaction are collected and validated, we are able to sign block
                _logger.LogInformation("Send prepare response");

                var mySignature = _blockManager.Sign(_context.GetProposedHeader(), _context.KeyPair);
                SignAndBroadcast(_context.MakePrepareResponse(mySignature));

                _context.State |= ConsensusState.SignatureSent;
                OnSignatureAcquired(_context.MyIndex, mySignature);

                lock (_quorumSignaturesAcquired)
                {
                    while (!IsQuorumReached())
                    {
                        // TODO: manage timeouts
                        var timeToWait = TimeUtils.Multiply(_timePerBlock, 1 + _context.MyState.ExpectedViewNumber);
                        if (Monitor.Wait(_quorumSignaturesAcquired, timeToWait))
                            continue;
                        _logger.LogWarning("Cannot retrieve all signatures in time, aborting");
                        RequestChangeView();
                        break;
                    }
                }

                if (_context.State.HasFlag(ConsensusState.ViewChanging))
                    continue;

                _logger.LogInformation(
                    $"Collected sinatures={_context.SignaturesAcquired}, quorum={_context.Quorum}"
                );
                // TODO: check multisig one last time

                var block = _context.GetProposedBlock();

                _logger.LogInformation($"Block approved by consensus: {block.Hash}");

                _context.State |= ConsensusState.BlockSent;

                var result = _blockManager.Persist(block);
                if (result == OperatingError.Ok)
                    _logger.LogInformation($"Block persist completed: {block.Hash}");
                else
                    _logger.LogWarning($"Block hasn't been persisted: {block.Hash}, cuz error {result}");

                _logger.LogInformation($"Block persist completed: {block.Hash}");
                _context.LastBlockRecieved = DateTime.UtcNow;
                InitializeConsensus(0);
            }
        }

        public void Start()
        {
            _logger.LogInformation("Starting consensus");
            Task.Factory.StartNew(_TaskWorker);
        }

        private void OnTimer(object sender, ElapsedEventArgs e)
        {
            lock (_timeToProduceBlock)
            {
                Monitor.PulseAll(_timeToProduceBlock);
            }
        }

        private void InitializeConsensus(byte viewNumber)
        {
            if (viewNumber == 0)
                _context.ResetState(_blockchainContext.CurrentBlock.Hash,
                    _blockchainContext.CurrentBlock.Header.Index);
            else
                _context.ChangeView(viewNumber);
            if (_context.MyIndex < 0) return;
            _logger.LogInformation(
                $"Initialized consensus: height={_context.BlockIndex} view={viewNumber} " +
                $"my_index={_context.MyIndex} role={_context.Role}"
            );

            if (!_context.Role.HasFlag(ConsensusState.Primary))
            {
                _context.State |= ConsensusState.Backup;
                return;
            }

            _context.State |= ConsensusState.Primary;
            var span = DateTime.UtcNow - _context.LastBlockRecieved;
            if (span >= _timePerBlock) OnTimer(null, null);
            else
            {
                _timer = new Timer((_timePerBlock - span).TotalMilliseconds);
                _timer.Elapsed += OnTimer;
            }
        }

        private void OnPrepareRequestReceived(ConsensusPayload payload)
        {
            if (_context.State.HasFlag(ConsensusState.ViewChanging))
            {
                _logger.LogDebug(
                    $"Ignoring prepare request from validator={payload.ValidatorIndex}: we are changing view"
                );
                return;
            }

            if (_context.State.HasFlag(ConsensusState.RequestReceived))
            {
                _logger.LogDebug(
                    $"Ignoring prepare request from validator={payload.ValidatorIndex}: we are already prepared"
                );
                return;
            }

            if (payload.ValidatorIndex != _context.PrimaryIndex)
            {
                _logger.LogDebug(
                    $"Ignoring prepare request from validator={payload.ValidatorIndex}: validator is not primary"
                );
                return;
            }

            if (payload.MessageCase != ConsensusPayload.MessageOneofCase.PrepareRequest)
            {
                _logger.LogDebug(
                    $"Ignoring prepare request from validator={payload.ValidatorIndex}: request is empty"
                );
                return;
            }

            var prepareRequest = payload.PrepareRequest;
            _logger.LogInformation(
                $"{nameof(OnPrepareRequestReceived)}: height={payload.BlockIndex} view={payload.ViewNumber} " +
                $"index={payload.ValidatorIndex} tx={prepareRequest.TransactionHashes.Count}"
            );
            if (!_context.State.HasFlag(ConsensusState.Backup))
            {
                _logger.LogDebug(
                    $"Ignoring prepare request from validator={payload.ValidatorIndex}: were are primary"
                );
                return;
            }

            /* TODO: block timestamping policy
            if (payload.Timestamp <= _blockchainContext.CurrentBlockHeader.Header.Timestamp ||
                payload.Timestamp > (ulong) DateTime.UtcNow.AddMinutes(10).ToTimestamp().Seconds)
            {
                _logger.LogDebug(
                    $"Ignoring prepare request from validator={payload.ValidatorIndex}: " +
                    $"timestamp incorrect: theirs={payload.Timestamp} ours={_context.Timestamp} " +
                    $"last_block={_blockchainContext.CurrentBlockHeader.Header.Timestamp}"
                );
                return;
            }*/

            _context.CurrentProposal = new ConsensusProposal
            {
                TransactionHashes = prepareRequest.TransactionHashes.ToArray(),
                Transactions = new Dictionary<UInt256, SignedTransaction>(),
                Timestamp = prepareRequest.Timestamp,
                Nonce = prepareRequest.Nonce
            };

            var header = _context.GetProposedHeader();
            var sigVerified = _blockManager.VerifySignature(header, prepareRequest.Signature,
                _context.Validators[payload.ValidatorIndex].PublicKey);
            if (sigVerified != OperatingError.Ok)
            {
                _logger.LogWarning(
                    $"Ignoring prepare request from validator={payload.ValidatorIndex}: " +
                    "request signature is invalid"
                );
                return;
            }

            _context.State |= ConsensusState.RequestReceived;

            OnSignatureAcquired(payload.ValidatorIndex, prepareRequest.Signature);
            _logger.LogInformation(
                $"Prepare request from validator={payload.ValidatorIndex} accepted, requesting missing transactions"
            );

            lock (_prepareRequestReceived)
            {
                Monitor.PulseAll(_prepareRequestReceived);
            }
        }

        public void HandleConsensusMessage(ConsensusMessage message)
        {
            if (_stopped)
            {
                _logger.LogWarning(
                    $"Cannot handle consensus payload from validator={message.Payload.ValidatorIndex}: " +
                    "consensus is stopped"
                );
                return;
            }
            var body = message.Payload;
            if (_context.State.HasFlag(ConsensusState.BlockSent)) return;
            if (body.ValidatorIndex == _context.MyIndex) return;
            if (body.Version != ConsensusContext.Version) return;

            var sigVerified = _crypto.VerifySignature(
                message.Payload.ToHash256().ToByteArray(),
                message.Signature.Buffer.ToByteArray(),
                _context.Validators[message.Payload.ValidatorIndex].PublicKey.Buffer.ToByteArray()
            );
            if (!sigVerified)
            {
                _logger.LogWarning(
                    $"Cannot handle consensus payload from validator={message.Payload.ValidatorIndex}: " +
                    "message signature is invalid"
                );
                return;
            }

            if (!body.PrevHash.Equals(_context.PreviousBlockHash) || body.BlockIndex != _context.BlockIndex)
            {
                _logger.LogWarning(
                    $"Cannot handle consensus payload from validator={message.Payload.ValidatorIndex} " +
                    $"at height={body.BlockIndex}, since " +
                    $"local height={_blockchainContext.CurrentBlockHeader.Header.Index}"
                );
                if (_blockchainContext.CurrentBlockHeader.Header.Index + 1 < body.BlockIndex)
                {
                    return;
                }

                _logger.LogWarning(
                    $"Rejected consensus payload from validator={message.Payload.ValidatorIndex} " +
                    $"because of prev hash mismatch");
                return;
            }

            if (body.ValidatorIndex >= _context.ValidatorCount) return;

            if (body.ViewNumber != _context.ViewNumber &&
                body.MessageCase != ConsensusPayload.MessageOneofCase.ChangeView)
            {
                _logger.LogWarning(
                    $"Rejected consensus payload of type {body.MessageCase} because view does not match, " +
                    $"my={_context.ViewNumber} theirs={body.ViewNumber} validator={message.Payload.ValidatorIndex}"
                );
                return;
            }

            _logger.LogInformation($"Received consensus payload from validator={message.Payload.ValidatorIndex} " +
                                   $"of type {body.MessageCase}");
            switch (body.MessageCase)
            {
                case ConsensusPayload.MessageOneofCase.ChangeView:
                    OnChangeViewReceived(body);
                    break;
                case ConsensusPayload.MessageOneofCase.PrepareRequest:
                    OnPrepareRequestReceived(body);
                    break;
                case ConsensusPayload.MessageOneofCase.PrepareResponse:
                    OnPrepareResponseReceived(body);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void OnPrepareResponseReceived(ConsensusPayload message)
        {
            if (_context.Validators[message.ValidatorIndex].BlockSignature != null) return;
            _logger.LogInformation(
                $"{nameof(OnPrepareResponseReceived)}: height={message.BlockIndex} view={message.ViewNumber} " +
                $"index={message.ValidatorIndex}"
            );
            OnSignatureAcquired(message.ValidatorIndex, message.PrepareResponse.Signature);
        }

        private void OnSignatureAcquired(long validatorIndex, Signature signature)
        {
            lock (_quorumSignaturesAcquired)
            {
                if (_context.Validators[validatorIndex].BlockSignature != null)
                    return;
                _context.Validators[validatorIndex].BlockSignature = signature;
                _context.SignaturesAcquired++;
                if (!IsQuorumReached())
                    return;
                Monitor.PulseAll(_quorumSignaturesAcquired);
            }
        }

        private bool IsQuorumReached()
        {
            return _context.SignaturesAcquired >= _context.Quorum;
        }

        private void RequestChangeView()
        {
            _context.State |= ConsensusState.ViewChanging;
            _context.MyState.ExpectedViewNumber++;
            _logger.LogInformation(
                $"request change view: height={_context.BlockIndex} view={_context.ViewNumber} " +
                $"nv={_context.MyState.ExpectedViewNumber} state={_context.State}"
            );
            SignAndBroadcast(_context.MakeChangeView());
            CheckExpectedView();
        }

        private void OnChangeViewReceived(ConsensusPayload payload)
        {
            var changeView = payload.ChangeView;
            if (changeView.NewViewNumber <= _context.Validators[payload.ValidatorIndex].ExpectedViewNumber)
            {
                _logger.LogInformation(
                    $"Ignoring ChangeView payload from validator={payload.ValidatorIndex} view={payload.ViewNumber} " +
                    $"since new_view={changeView.NewViewNumber} and " +
                    $"last_view={_context.Validators[payload.ValidatorIndex].ExpectedViewNumber}"
                );
                return;
            }

            _logger.LogInformation(
                $"{nameof(OnChangeViewReceived)}: height={payload.BlockIndex} view={payload.ViewNumber} " +
                $"index={payload.ValidatorIndex} nv={changeView.NewViewNumber}"
            );
            _context.Validators[payload.ValidatorIndex].ExpectedViewNumber = (byte) changeView.NewViewNumber;
            CheckExpectedView();
        }

        private void CheckExpectedView()
        {
            lock (_changeViewApproved)
            {
                if (!CanChangeView(out _))
                    return;
                /*_context.ViewNumber = viewNumber;*/
                Monitor.PulseAll(_changeViewApproved);
            }
        }

        private bool CanChangeView(out byte viewNumber)
        {
            var mostCommon = _context.Validators
                .GroupBy(v => v.ExpectedViewNumber)
                .OrderByDescending(v => v.Count())
                .Select(v => v.Key)
                .First();
            viewNumber = mostCommon;
            if (_context.ViewNumber == viewNumber)
                return false;
            if (_context.Validators.Select(v => v.ExpectedViewNumber).Count(p => p == mostCommon) < _context.Quorum)
                return false;
            return true;
        }

        private void SignAndBroadcast(ConsensusPayload payload)
        {
            var message = new ConsensusMessage
            {
                Payload = payload,
                Signature = _crypto
                    .Sign(payload.ToHash256().ToByteArray(), _context.KeyPair.PrivateKey.Buffer.ToByteArray())
                    .ToSignature()
            };
            _broadcaster.Broadcast(new Message
            {
                Type = MessageType.ConsensusMessage,
                ConsensusMessage = message
            });
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}