using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Org.BouncyCastle.Security;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Blockchain.Pool;
using Phorkus.Core.Config;
using Phorkus.Core.Cryptography;
using Phorkus.Core.Logging;
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
        private readonly IConsensusService _consensusService;
        private readonly ISynchronizer _synchronizer;

        private readonly object _quorumSignaturesAcquired = new object();
        private readonly object _prepareRequestReceived = new object();
        private readonly object _changeViewApproved = new object();
        private Timer _timer;
        private bool _stopped;
        private bool _gotNewBlock;
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
            ISynchronizer blockchainSynchronizer,
            IConsensusService consensusService)
        {
            var config = configManager.GetConfig<ConsensusConfig>("consensus");
            _blockManager = blockManager ?? throw new ArgumentNullException(nameof(blockManager));
            _transactionManager = transactionManager ?? throw new ArgumentNullException(nameof(transactionManager));
            _transactionPool = transactionPool ?? throw new ArgumentNullException(nameof(transactionPool));
            _transactionFactory = transactionFactory ?? throw new ArgumentNullException(nameof(transactionFactory));
            _synchronizer =
                blockchainSynchronizer ?? throw new ArgumentNullException(nameof(blockchainSynchronizer));
            _consensusService = consensusService;
            _blockchainContext = blockchainContext ?? throw new ArgumentNullException(nameof(blockchainContext));
            _broadcaster = broadcaster ?? throw new ArgumentNullException(nameof(broadcaster));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _crypto = crypto ?? throw new ArgumentNullException(nameof(crypto));

            _keyPair = new KeyPair(config.PrivateKey.HexToBytes().ToPrivateKey(), crypto);
            _context = new ConsensusContext(_keyPair,
                config.ValidatorsKeys.Select(key => key.HexToBytes().ToPublicKey()).ToList());
            _random = new SecureRandom();
            _stopped = true;
            _gotNewBlock = false;

            _blockManager.OnBlockPersisted += OnBlockPersisted;
        }

        private void OnBlockPersisted(object sender, Block e)
        {
            lock (this)
            {
                _context.LastBlockRecieved = DateTime.UtcNow;
                if (_context.State.HasFlag(ConsensusState.ViewChanging))
                    _gotNewBlock = true;
            }
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
                            var timeToWait = TimeUtils.Multiply(_timePerBlock, _context.MyState.ExpectedViewNumber);
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
                    var timeToAwait = _timePerBlock - (DateTime.UtcNow - _context.LastBlockRecieved);
                    if (timeToAwait.TotalSeconds > 0)
                        Thread.Sleep(timeToAwait);

                    // TODO: produce block
                    var blockBuilder = new BlockBuilder(_transactionPool, _blockchainContext.CurrentBlockHeader.Hash,
                        _blockchainContext.CurrentBlockHeader.Header.Index);
                    var minerTx = _transactionFactory.MinerTransaction(
                        _crypto.ComputeAddress(_context.KeyPair.PublicKey.Buffer.ToByteArray()).ToUInt160());
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

                    /* TODO: "get prepare replies from validators" */
                    _broadcaster.ConsensusService.PrepareBlock(
                        _context.MakePrepareRequest(blockWithTransactions, _context.MyState.BlockSignature), _keyPair);
                    _logger.LogInformation("Sent prepare request");
//                    -- code for handling prepare reponse --
//                    if (_context.Validators[message.ValidatorIndex].BlockSignature != null) return;
//                    _logger.LogInformation(
//                        $"{nameof(OnPrepareResponseReceived)}: height={message.BlockIndex} view={message.ViewNumber} " +
//                        $"index={message.ValidatorIndex}"
//                    );
//                    OnSignatureAcquired(message.ValidatorIndex, message.PrepareResponse.Signature);
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
                    _synchronizer.WaitForTransactions(_context.CurrentProposal.TransactionHashes,
                        _timePerBlock);
                if (txsGot != _context.CurrentProposal.TransactionHashes.Length)
                {
                    _logger.LogWarning(
                        $"Cannot retrieve all transactions in time, got only {txsGot} of {_context.CurrentProposal.TransactionHashes.Length}, aborting");
                    RequestChangeView();
                    continue;
                }

                // When all transaction are collected and validated, we are able to sign block
                _logger.LogInformation("Sent prepare response");

                var mySignature = _blockManager.Sign(_context.GetProposedHeader(), _context.KeyPair);
                /* TODO: "try to manage block prepare responses here" */
//                SignAndBroadcast(_context.MakePrepareResponse(mySignature));

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
        }

        public bool CanHandleConsensusMessage(Validator validator, IMessage message)
        {
            if (_stopped)
            {
                _logger.LogWarning(
                    $"Cannot handle consensus payload from validator={validator.ValidatorIndex}: consensus is stopped"
                );
                return false;
            }

            if (_context.State.HasFlag(ConsensusState.BlockSent))
            {
                _logger.LogTrace(
                    $"Cannot handle consensus payload from validator={validator.ValidatorIndex}: block has already been sent"
                );
                return false;
            }

            if (validator.ValidatorIndex == _context.MyIndex)
            {
                _logger.LogWarning(
                    $"Cannot handle consensus payload from validator={validator.ValidatorIndex}: invalid validator index"
                );
                return false;
            }

            if (validator.Version != ConsensusContext.Version)
            {
                _logger.LogWarning(
                    $"Cannot handle consensus payload from validator={validator.ValidatorIndex}: invalid version specified"
                );
                return false;
            }

            if (!validator.PrevHash.Equals(_context.PreviousBlockHash) || validator.BlockIndex != _context.BlockIndex)
            {
                _logger.LogWarning(
                    $"Cannot handle consensus payload from validator={validator.ValidatorIndex} at height={validator.BlockIndex}, since local height={_blockchainContext.CurrentBlockHeader.Header.Index}"
                );
                if (_blockchainContext.CurrentBlockHeader.Header.Index + 1 < validator.BlockIndex)
                    return false;
                _logger.LogWarning(
                    $"Rejected consensus payload from validator={validator.ValidatorIndex} because of prev hash mismatch");
                return false;
            }

            if (validator.ValidatorIndex >= _context.ValidatorCount)
            {
                _logger.LogWarning(
                    $"Cannot handle consensus payload from validator={validator.ValidatorIndex}: invalid validator index"
                );
                return false;
            }

            if (validator.ViewNumber != _context.ViewNumber && !(message is ChangeViewRequest))
            {
                _logger.LogWarning(
                    $"Rejected consensus payload of type {message.GetType()} because view does not match, my={_context.ViewNumber} theirs={validator.ViewNumber} validator={validator.ValidatorIndex}"
                );
                return false;
            }

            return true;
        }

        public void OnBlockPrepareRequestReceived(BlockPrepareRequest blockPrepare)
        {
            var validator = blockPrepare.Validator;

            if (_context.State.HasFlag(ConsensusState.ViewChanging))
            {
                _logger.LogDebug(
                    $"Ignoring prepare request from validator={validator.ValidatorIndex}: we are changing view"
                );
                return;
            }

            if (_context.State.HasFlag(ConsensusState.RequestReceived))
            {
                _logger.LogDebug(
                    $"Ignoring prepare request from validator={validator.ValidatorIndex}: we are already prepared"
                );
                return;
            }

            if (validator.ValidatorIndex != _context.PrimaryIndex)
            {
                _logger.LogDebug(
                    $"Ignoring prepare request from validator={validator.ValidatorIndex}: validator is not primary"
                );
                return;
            }

            _logger.LogInformation(
                $"{nameof(OnBlockPrepareRequestReceived)}: height={validator.BlockIndex} view={validator.ViewNumber} " +
                $"index={validator.ValidatorIndex} tx={blockPrepare.TransactionHashes.Count}"
            );
            if (!_context.State.HasFlag(ConsensusState.Backup))
            {
                _logger.LogDebug(
                    $"Ignoring prepare request from validator={validator.ValidatorIndex}: were are primary"
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
                TransactionHashes = blockPrepare.TransactionHashes.ToArray(),
                Transactions = new Dictionary<UInt256, SignedTransaction>(),
                Timestamp = blockPrepare.Timestamp,
                Nonce = blockPrepare.Nonce
            };

            var header = _context.GetProposedHeader();
            var sigVerified = _blockManager.VerifySignature(header, blockPrepare.Signature,
                _context.Validators[validator.ValidatorIndex].PublicKey);
            if (sigVerified != OperatingError.Ok)
            {
                _logger.LogWarning(
                    $"Ignoring prepare request from validator={validator.ValidatorIndex}: " +
                    "request signature is invalid"
                );
                return;
            }

            _context.State |= ConsensusState.RequestReceived;

            OnSignatureAcquired(validator.ValidatorIndex, blockPrepare.Signature);
            _logger.LogInformation(
                $"Prepare request from validator={validator.ValidatorIndex} accepted, requesting missing transactions"
            );

            lock (_prepareRequestReceived)
            {
                Monitor.PulseAll(_prepareRequestReceived);
            }
        }

        public void OnChangeViewReceived(ChangeViewRequest changeView)
        {
            var validator = changeView.Validator;
            if (changeView.NewViewNumber <= _context.Validators[validator.ValidatorIndex].ExpectedViewNumber)
            {
                _logger.LogInformation(
                    $"Ignoring ChangeView payload from validator={validator.ValidatorIndex} view={validator.ViewNumber} " +
                    $"since new_view={changeView.NewViewNumber} and " +
                    $"last_view={_context.Validators[validator.ValidatorIndex].ExpectedViewNumber}"
                );
                return;
            }
            _logger.LogInformation(
                $"{nameof(OnChangeViewReceived)}: height={validator.BlockIndex} view={validator.ViewNumber} " +
                $"index={validator.ValidatorIndex} nv={changeView.NewViewNumber}"
            );
            _context.Validators[validator.ValidatorIndex].ExpectedViewNumber = (byte) changeView.NewViewNumber;
            CheckExpectedView();
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
            /* TODO: "handle responses from clients" */
            _broadcaster.ConsensusService.ChangeView(_context.MakeChangeView(), _keyPair);
            CheckExpectedView();
        }

        private void CheckExpectedView()
        {
            _logger.LogTrace(
                $"Current view profile: {string.Join(" ", _context.Validators.Select(validator => validator.ExpectedViewNumber))}");
            lock (_changeViewApproved)
            {
                if (!CanChangeView(out _))
                    return;
                /*_context.ViewNumber = viewNumber;*/
                _logger.LogTrace("We are ready to change view!");
                Monitor.PulseAll(_changeViewApproved);
            }
        }

        private bool CanChangeView(out byte viewNumber)
        {
            lock (this)
            {
                if (_gotNewBlock)
                {
                    _gotNewBlock = false;
                    viewNumber = 0;
                    return true;
                }
            }

            var mostCommon = _context.Validators
                .GroupBy(v => v.ExpectedViewNumber)
                .OrderByDescending(v => v.Count())
                .Select(v => v.Key)
                .First();
            viewNumber = mostCommon;
            if (_context.ViewNumber == viewNumber)
                return false;
            return _context.Validators.Select(v => v.ExpectedViewNumber).Count(p => p == mostCommon) >= _context.Quorum;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}