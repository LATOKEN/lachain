using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using NeoSharp.Core.Blockchain;
using NeoSharp.Core.Blockchain.Processing;
using NeoSharp.Core.Blockchain.Processing.BlockProcessing;
using NeoSharp.Core.Consensus.Config;
using NeoSharp.Core.Consensus.Messages;
using NeoSharp.Core.Extensions;
using NeoSharp.Core.Logging;
using NeoSharp.Core.Messaging.Messages;
using NeoSharp.Core.Models;
using NeoSharp.Core.Network;
using NeoSharp.Types;
using NeoSharp.Types.ExtensionMethods;
using Org.BouncyCastle.Security;

namespace NeoSharp.Core.Consensus
{
    // ReSharper disable once RedundantNameQualifier
    using Timer = System.Timers.Timer;

    public class ConsensusManager : IConsensusManager, IDisposable
    {
        private readonly IBlockProcessor _blockProcessor;
        private readonly IBlockchainContext _blockchainContext;
        private readonly ITransactionCrawler _transactionCrawler;
        private readonly ITransactionPool _transactionPool;
        private readonly IBlockProducer _blockProducer;
        private readonly IBroadcaster _broadcaster;
        private readonly ILogger<ConsensusManager> _logger;
        private readonly ConsensusContext _context;
        private readonly object _allTransactionVerified = new object();
        private readonly object _quorumSignaturesAcquired = new object();
        private readonly object _prepareRequestReceived = new object();
        private readonly object _changeViewApproved = new object();
        private readonly object _timeToProduceBlock = new object();
        private Timer _timer;
        private bool _stopped;
        private SecureRandom _random;

        private readonly TimeSpan _timePerBlock = TimeSpan.FromSeconds(15);
        private const int MaxTransactionsInBlock = 1000;

        public ConsensusManager(
            IBlockProcessor blockProcessor, IBlockchainContext blockchainContext,
            ITransactionCrawler transactionCrawler, ITransactionProcessor transactionProcessor,
            ITransactionPool transactionPool, IBlockProducer blockProducer, IBroadcaster broadcaster,
            ILogger<ConsensusManager> logger, ConsensusConfig configuration
        )
        {
            _blockProcessor = blockProcessor ?? throw new ArgumentNullException(nameof(blockProcessor));
            _blockchainContext = blockchainContext ?? throw new ArgumentNullException(nameof(blockProcessor));
            _transactionCrawler = transactionCrawler ?? throw new ArgumentNullException(nameof(transactionCrawler));
            _transactionPool = transactionPool ?? throw new ArgumentNullException(nameof(transactionPool));
            _blockProducer = blockProducer;
            _broadcaster = broadcaster;
            _logger = logger ?? throw new ArgumentNullException(nameof(blockProcessor));
            _context = new ConsensusContext(configuration.KeyPair, configuration.ValidatorsKeys);
            _random = new SecureRandom();

            (transactionProcessor ?? throw new ArgumentNullException(nameof(transactionProcessor)))
                .OnTransactionProcessed += OnTransactionVerified;
        }

        public void Stop()
        {
            _stopped = true;
        }

        private void _TaskWorker()
        {
            InitializeConsensus(0);
            _context.Timestamp = _blockchainContext.CurrentBlock.Timestamp;
            Thread.Sleep(1000);
            while (!_stopped)
            {
                // If were are waiting for view change, just wait
                if (_context.State.HasFlag(ConsensusState.ViewChanging))
                {
                    lock (_changeViewApproved)
                    {
                        // TODO: manage timeouts
                        var timeToWait = _timePerBlock * (1 + _context.MyState.ExpectedViewNumber);
                        if (!Monitor.Wait(_changeViewApproved, timeToWait))
                        {
                            RequestChangeView();
                            continue;
                        }

                        InitializeConsensus(_context.ViewNumber);
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
                    var newBlock = _blockProducer.ProduceBlock(
                        MaxTransactionsInBlock, DateTime.UtcNow, (ulong) _random.Next(),
                        _context.MyState.PublicKey.EncodedData.ToScriptHash()
                    );
                    _logger.LogInformation($"Produced block with hash {newBlock.Hash}");
                    _context.UpdateCurrentProposal(newBlock);
                    _context.State |= ConsensusState.RequestSent;
                    if (!_context.State.HasFlag(ConsensusState.SignatureSent))
                    {
                        _context.MyState.BlockSignature = "0xbadcab1e".HexToBytes();
                        //_context.MyState.BlockSignature = newBlock.GetBlockHeader().Sign(_context.);
                    }

                    SignAndBroadcast(_context.MakePrepareRequest(newBlock));
                    _logger.LogInformation("Sent prepare request");
                }
                else
                {
                    // if we are backup, wait unitl someone sends prepare, or change view
                    lock (_prepareRequestReceived)
                    {
                        var timeToWait = _timePerBlock * (1 + _context.ViewNumber); // TODO: manage timeouts
                        if (!Monitor.Wait(_prepareRequestReceived, timeToWait))
                        {
                            RequestChangeView();
                            continue;
                        }
                    }
                }

                _context.CurrentProposal.TransactionHashes.ForEach(hash =>
                {
                    var transaction = _transactionPool.FindByHash(hash);
                    if (transaction != null) _context.CurrentProposal.Transactions[hash] = transaction;
                    else _transactionCrawler.AddTransactionHash(hash);
                });
                lock (_allTransactionVerified)
                {
                    if (!Monitor.Wait(_allTransactionVerified, _timePerBlock)) // TODO: manage timeouts
                    {
                        _logger.LogWarning("Cannot retrieve all transactions in time, aborting");
                        RequestChangeView();
                        continue;
                    }
                }

                _logger.LogInformation("Send prepare response");
                _context.State |= ConsensusState.SignatureSent;
                _context.MyState.BlockSignature = "0xbadcab1e".HexToBytes();
                //_context.Validators[_context.MyIndex].BlockSignature = _context.GetProposedHeader().Sign(context.KeyPair);
                SignAndBroadcast(_context.MakePrepareResponse(_context.MyState.BlockSignature));
                OnSignatureAcquired(_context.MyIndex, "0xbadcab1e".HexToBytes());
                lock (_quorumSignaturesAcquired)
                {
                    var timeToWait = _timePerBlock * (1 + _context.ViewNumber); // TODO: manage timeouts
                    if (!Monitor.Wait(_quorumSignaturesAcquired, timeToWait))
                    {
                        _logger.LogWarning("Cannot retrieve all signatures in time, aborting");
                        RequestChangeView();
                        continue;
                    }
                }

                _logger.LogInformation(
                    $"Collected sinatures={_context.SignaturesAcquired}, quorum={_context.Quorum}"
                );

                var block = _context.GetProposedBlock();
                /* TODO: multisig
                ContractParametersContext sc = new ContractParametersContext(block);
                for (int i = 0, j = 0; i < context.Validators.Length && j < context.M; i++)
                    if (context.Signatures[i] != null)
                    {
                        sc.AddSignature(contract, context.Validators[i], context.Signatures[i]);
                        j++;
                    }
                sc.Verifiable.Witnesses = sc.GetWitnesses();
                */
                _logger.LogInformation($"Block approved by consensus: {block.Hash}");

                _context.State |= ConsensusState.BlockSent;
                _blockProcessor.AddBlock(block).Start(); // ??
                _blockProcessor.WaitUntilBlockProcessed(block.Index);

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
                _context.ResetState(_blockchainContext.CurrentBlock.Hash, _blockchainContext.CurrentBlock.Index);
            else
                _context.ChangeView(viewNumber);
            if (_context.MyIndex < 0) return;
            _logger.LogInformation(
                $"Initialized consensus: height={_context.BlockIndex} view={viewNumber} my_index={_context.MyIndex} role={_context.Role}");

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

        private void OnPrepareRequestReceived(ConsensusPayloadUnsigned message, PrepareRequest request)
        {
            if (_context.State.HasFlag(ConsensusState.ViewChanging))
            {
                _logger.LogDebug(
                    $"Ignoring prepare request from validator={message.ValidatorIndex}: we are changing view"
                );
                return;
            }
            if (_context.State.HasFlag(ConsensusState.RequestReceived))
            {
                _logger.LogDebug(
                    $"Ignoring prepare request from validator={message.ValidatorIndex}: we are already prepared"
                );
                return;
            }

            if (message.ValidatorIndex != _context.PrimaryIndex)
            {
                _logger.LogDebug(
                    $"Ignoring prepare request from validator={message.ValidatorIndex}: validator is not primary"
                );
                return;
            }

            _logger.LogInformation(
                $"{nameof(OnPrepareRequestReceived)}: height={message.BlockIndex} view={request.ViewNumber} " +
                $"index={message.ValidatorIndex} tx={request.TransactionHashes.Length}"
            );
            if (!_context.State.HasFlag(ConsensusState.Backup))
            {
                _logger.LogDebug(
                    $"Ignoring prepare request from validator={message.ValidatorIndex}: were are primary"
                );
                return;
            }

            if (message.Timestamp <= _blockchainContext.LastBlockHeader.Timestamp ||
                message.Timestamp > DateTime.UtcNow.AddMinutes(10).ToTimestamp())
            {
                _logger.LogDebug(
                    $"Ignoring prepare request from validator={message.ValidatorIndex}: " +
                    $"timestamp incorrect: theirs={message.Timestamp} ours={_context.Timestamp} " +
                    $"last_block={_blockchainContext.LastBlockHeader.Timestamp}"
                );
                return;
            }

            _context.State |= ConsensusState.RequestReceived;
            _context.Timestamp = message.Timestamp;
            _context.Nonce = request.Nonce; // TODO: we are blindly accepting their nonce!
            _context.CurrentProposal = new ConsensusProposal
            {
                TransactionHashes = request.TransactionHashes,
                Transactions = new Dictionary<UInt256, Transaction>()
            };

            /* TODO: check signature
            byte[] hashData = BinarySerializer.Default.Serialize(_context.GetProposedHeader().Hash);
             if (!Crypto.Default.VerifySignature(hashData, request.Signature,
                 _context.Validators[message.ValidatorIndex].PublicKey.DecodedData))
            {
                return;
            }
            for (int i = 0; i < context.Signatures.Length; i++)
                if (context.Signatures[i] != null)
                    if (!Crypto.Default.VerifySignature(hashData, context.Signatures[i],
                        context.Validators[i].EncodePoint(false)))
                        context.Signatures[i] = null;
            */
            OnSignatureAcquired(message.ValidatorIndex, request.Signature);
            _logger.LogInformation(
                $"Prepare request from validator={message.ValidatorIndex} accepted, requesting missing transactions"
            );
        }

        public void HandleConsensusMessage(ConsensusMessage message)
        {
            var body = message.Payload.Unsigned;
            if (_context.State.HasFlag(ConsensusState.BlockSent)) return;
            if (body.ValidatorIndex == _context.MyIndex) return;
            if (body.Version != ConsensusContext.Version) return;
            if (body.PrevHash != _context.PreviousBlockHash || body.BlockIndex != _context.BlockIndex)
            {
                _logger.LogWarning(
                    $"Cannot handle consensus payload at height={body.BlockIndex}, " +
                    $"local height={_blockchainContext.CurrentBlock.Index}"
                );
                if (_blockchainContext.CurrentBlock.Index + 1 < body.BlockIndex)
                {
                    return;
                }

                _logger.LogWarning("Rejected consensus payload because of prev hash mismatch");
                return;
            }

            if (body.ValidatorIndex >= _context.ValidatorCount) return;

            ConsensusPayloadCustomData data;
            try
            {
                data = ConsensusPayloadCustomData.DeserializeFrom(body.Data);
            }
            catch (Exception e)
            {
                _logger.LogWarning("Rejected consensus payload because of deserialization error");
                _logger.LogDebug($"Exception {e} while deserializing payload={body.Data}");
                _logger.LogDebug(e.StackTrace);
                return;
            }

            if (data.ViewNumber != _context.ViewNumber && data.Type != ConsensusMessageType.ChangeView)
            {
                _logger.LogWarning(
                    $"Rejected consensus payload of type {data.Type} because view does not match, " +
                    $"my={_context.ViewNumber} theirs={data.ViewNumber}"
                );
                return;
            }

            _logger.LogInformation($"Received consensus payload of type {data.Type}");
            switch (data.Type)
            {
                case ConsensusMessageType.ChangeView:
                    OnChangeViewReceived(body, (ChangeView) data);
                    break;
                case ConsensusMessageType.PrepareRequest:
                    OnPrepareRequestReceived(body, (PrepareRequest) data);
                    break;
                case ConsensusMessageType.PrepareResponse:
                    OnPrepareResponseReceived(body, (PrepareResponse) data);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void OnPrepareResponseReceived(ConsensusPayloadUnsigned message, PrepareResponse request)
        {
            if (_context.Validators[message.ValidatorIndex].BlockSignature != null) return;
            _logger.LogInformation(
                $"{nameof(OnPrepareResponseReceived)}: height={message.BlockIndex} view={request.ViewNumber} " +
                $"index={message.ValidatorIndex}"
            );
            OnSignatureAcquired(message.ValidatorIndex, request.Signature);
        }

        private bool OnSignatureAcquired(int validatorIndex, byte[] signature)
        {
            if (_context.Validators[validatorIndex].BlockSignature != null) return false;
            // TODO: verify signature
            //byte[] hashData = _context.GetProposedHeader()?.GetHashData();
            //if (Crypto.Default.VerifySignature(hashData, message.Signature,
            //    context.Validators[payload.ValidatorIndex].EncodePoint(false))) ...
            _context.Validators[validatorIndex].BlockSignature = signature;
            _context.SignaturesAcquired++;
            if (_context.SignaturesAcquired >= _context.Quorum)
            {
                lock (_quorumSignaturesAcquired)
                {
                    Monitor.PulseAll(_quorumSignaturesAcquired);
                }
            }

            return true;
        }

        private void OnTransactionVerified(object sender, Transaction e)
        {
            if (_context.CurrentProposal.Transactions.ContainsKey(e.Hash)) return;
            _context.CurrentProposal.Transactions[e.Hash] = e;
            if (!_context.CurrentProposal.IsComplete) return;
            lock (_allTransactionVerified)
            {
                Monitor.PulseAll(_allTransactionVerified);
            }
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
            CheckExpectedView(_context.MyState.ExpectedViewNumber);
        }

        private void OnChangeViewReceived(ConsensusPayloadUnsigned messageBody, ChangeView changeView)
        {
            if (changeView.NewViewNumber <= _context.Validators[messageBody.ValidatorIndex].ExpectedViewNumber)
                return;
            _logger.LogInformation(
                $"{nameof(OnChangeViewReceived)}: height={messageBody.BlockIndex} view={changeView.ViewNumber} " +
                $"index={messageBody.ValidatorIndex} nv={changeView.NewViewNumber}"
            );
            _context.Validators[messageBody.ValidatorIndex].ExpectedViewNumber = changeView.NewViewNumber;
            CheckExpectedView(changeView.NewViewNumber);
        }

        private void CheckExpectedView(byte viewNumber)
        {
            if (_context.ViewNumber == viewNumber) return;
            if (_context.Validators.Select(v => v.ExpectedViewNumber).Count(p => p == viewNumber) >= _context.Quorum)
            {
                lock (_changeViewApproved)
                {
                    _context.ViewNumber = viewNumber;
                    Monitor.PulseAll(_changeViewApproved);
                }
            }
        }

        private void SignAndBroadcast(ConsensusPayload payload)
        {
            // TODO: sign
            _broadcaster.Broadcast(new ConsensusMessage(payload));
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}