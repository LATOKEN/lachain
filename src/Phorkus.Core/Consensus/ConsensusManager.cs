﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Org.BouncyCastle.Security;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Config;
using Phorkus.Core.Network;
using Phorkus.Proto;
using Phorkus.Crypto;
using Phorkus.Logger;
using Phorkus.Networking;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.Consensus
{
    public class ConsensusManager : IConsensusManager
    {
        private readonly IBlockManager _blockManager;
        private readonly ITransactionManager _transactionManager;
        private readonly IBlockchainContext _blockchainContext;
        private readonly ITransactionPool _transactionPool;
        private readonly INetworkBroadcaster _broadcaster;
        private readonly ILogger<ConsensusManager> _logger;
        private readonly ITransactionBuilder _transactionBuilder;
        private readonly ICrypto _crypto;
        private readonly ConsensusContext _context;
        private readonly KeyPair _keyPair;
        private readonly IBlockSynchronizer _blockchainSynchronizer;
        private readonly MessageFactory _messageFactory;

        private readonly object _quorumSignaturesAcquired = new object();
        private readonly object _prepareRequestReceived = new object();
        private readonly object _changeViewApproved = new object();
        private bool _stopped;
        private bool _gotNewBlock;
        private readonly SecureRandom _random;

        private readonly TimeSpan _timePerBlock = TimeSpan.FromSeconds(15);

        public ConsensusManager(
            IBlockManager blockManager,
            ITransactionManager transactionManager,
            IBlockchainContext blockchainContext,
            ITransactionPool transactionPool,
            INetworkBroadcaster broadcaster,
            ILogger<ConsensusManager> logger,
            IConfigManager configManager,
            ITransactionBuilder transactionFactory,
            ICrypto crypto,
            IBlockSynchronizer blockchainSynchronizer)
        {
            var config = configManager.GetConfig<ConsensusConfig>("consensus");
            _blockManager = blockManager ?? throw new ArgumentNullException(nameof(blockManager));
            _transactionManager = transactionManager ?? throw new ArgumentNullException(nameof(transactionManager));
            _transactionPool = transactionPool ?? throw new ArgumentNullException(nameof(transactionPool));
            _transactionBuilder = transactionFactory ?? throw new ArgumentNullException(nameof(transactionFactory));
            _blockchainSynchronizer =
                blockchainSynchronizer ?? throw new ArgumentNullException(nameof(blockchainSynchronizer));
            _blockchainContext = blockchainContext ?? throw new ArgumentNullException(nameof(blockchainContext));
            _broadcaster = broadcaster ?? throw new ArgumentNullException(nameof(broadcaster));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _crypto = crypto ?? throw new ArgumentNullException(nameof(crypto));

            _keyPair = new KeyPair(config.PrivateKey.HexToBytes().ToPrivateKey(), crypto);
            _messageFactory = new MessageFactory(_keyPair, _crypto);
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
            Thread.Sleep(5000);

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
                            var timeToWait = TimeUtils.Multiply(_timePerBlock, 1 << _context.MyState.ExpectedViewNumber);
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
                    var timeToAwait = _timePerBlock - (DateTime.UtcNow - _context.LastBlockRecieved) - TimeSpan.FromSeconds(1);
                    _logger.LogTrace("Waiting " + timeToAwait.TotalMilliseconds + "ms until we can produce block");
                    if (timeToAwait.TotalSeconds > 0)
                        Thread.Sleep(timeToAwait);
                    _logger.LogTrace("Wait completed");

                    // TODO: produce block
                    var address = _crypto.ComputeAddress(_context.KeyPair.PublicKey.Buffer.ToByteArray());
                    var from = address.ToUInt160();
                    var minerTx = _transactionBuilder.MinerTransaction(from);
                    var signed = _transactionManager.Sign(minerTx, _keyPair);
                    var minerError = _transactionManager.Persist(signed);
                    if (minerError != OperatingError.Ok)
                    {
                        _logger.LogError(
                            $"Unable to persis miner transaction (it is very bad), cuz error {minerError}");
                        RequestChangeView();
                        continue;
                    }

                    var blockWithTransactions = new BlockBuilder(_blockchainContext.CurrentBlockHeader.Header)
                        .WithTransactions(_transactionPool).WithMiner(signed).Build((ulong) _random.Next());

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
                        OnSignatureAcquired((uint) _context.MyIndex, signature);
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
                        var timeToWait = TimeUtils.Multiply(_timePerBlock, 1 << _context.MyState.ExpectedViewNumber);
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
                OnSignatureAcquired((uint) _context.MyIndex, mySignature);

                lock (_quorumSignaturesAcquired)
                {
                    while (!IsQuorumReached())
                    {
                        // TODO: manage timeouts
                        var timeToWait = TimeUtils.Multiply(_timePerBlock, 1 << _context.MyState.ExpectedViewNumber);
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

        public void OnPrepareRequestReceived(BlockPrepareRequest prepareRequest)
        {
            if (!CheckPayload(prepareRequest.Validator, false)) return;
            uint validatorIndex = prepareRequest.Validator.ValidatorIndex;
            if (_context.State.HasFlag(ConsensusState.ViewChanging))
            {
                _logger.LogDebug(
                    $"Ignoring prepare request from validator={validatorIndex}: we are changing view"
                );
                return;
            }

            if (_context.State.HasFlag(ConsensusState.RequestReceived))
            {
                _logger.LogDebug(
                    $"Ignoring prepare request from validator={validatorIndex}: we are already prepared"
                );
                return;
            }

            if (validatorIndex != _context.PrimaryIndex)
            {
                _logger.LogDebug(
                    $"Ignoring prepare request from validator={validatorIndex}: validator is not primary"
                );
                return;
            }

            _logger.LogInformation(
                $"{nameof(OnPrepareRequestReceived)}: height={prepareRequest.Validator.BlockIndex} " +
                $"view={prepareRequest.Validator.ViewNumber}  index={validatorIndex} " +
                $"tx={prepareRequest.TransactionHashes.Count}"
            );
            if (!_context.State.HasFlag(ConsensusState.Backup))
            {
                _logger.LogDebug(
                    $"Ignoring prepare request from validator={validatorIndex}: were are primary"
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
                _context.Validators[validatorIndex].PublicKey);
            if (sigVerified != OperatingError.Ok)
            {
                _logger.LogWarning(
                    $"Ignoring prepare request from validator={validatorIndex}: " +
                    "request signature is invalid"
                );
                return;
            }

            _context.State |= ConsensusState.RequestReceived;

            OnSignatureAcquired(validatorIndex, prepareRequest.Signature);
            _logger.LogInformation(
                $"Prepare request from validator={validatorIndex} accepted, requesting missing transactions"
            );

            lock (_prepareRequestReceived)
            {
                Monitor.PulseAll(_prepareRequestReceived);
            }
        }

        public void OnPrepareResponseReceived(BlockPrepareReply prepareResponse)
        {
            if (!CheckPayload(prepareResponse.Validator, false)) return;
            if (_context.Validators[prepareResponse.Validator.ValidatorIndex].BlockSignature != null) return;
            _logger.LogInformation(
                $"{nameof(OnPrepareResponseReceived)}: height={prepareResponse.Validator.BlockIndex} view={prepareResponse.Validator.ViewNumber} " +
                $"index={prepareResponse.Validator.ValidatorIndex}"
            );
            OnSignatureAcquired(prepareResponse.Validator.ValidatorIndex, prepareResponse.Signature);
        }
        
        public void OnChangeViewReceived(ChangeViewRequest changeViewRequest)
        {
            if (!CheckPayload(changeViewRequest.Validator, false)) return;
            if (changeViewRequest.NewViewNumber <=
                _context.Validators[changeViewRequest.Validator.ValidatorIndex].ExpectedViewNumber)
            {
                _logger.LogInformation(
                    $"Ignoring ChangeView payload from validator={changeViewRequest.Validator.ValidatorIndex} " +
                    $"view={changeViewRequest.Validator.ViewNumber} " +
                    $"since new_view={changeViewRequest.NewViewNumber} and " +
                    $"last_view={_context.Validators[changeViewRequest.Validator.ValidatorIndex].ExpectedViewNumber}"
                );
                return;
            }

            _logger.LogInformation(
                $"{nameof(OnChangeViewReceived)}: height={changeViewRequest.Validator.BlockIndex} " +
                $"view={changeViewRequest.Validator.ViewNumber} " +
                $"index={changeViewRequest.Validator.ValidatorIndex} nv={changeViewRequest.NewViewNumber}"
            );
            _context.Validators[changeViewRequest.Validator.ValidatorIndex].ExpectedViewNumber =
                (byte) changeViewRequest.NewViewNumber;
            CheckExpectedView();
        }

        private void OnSignatureAcquired(uint validatorIndex, Signature signature)
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
            SignAndBroadcast(_context.MakeChangeViewRequest());
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

        private bool CheckPayload(Validator validator, bool isChangeView)
        {
            if (_stopped)
            {
                _logger.LogWarning(
                    $"Cannot handle consensus payload from validator={validator.ValidatorIndex}: " +
                    "consensus is stopped"
                );
                return false;
            }

            if (_context.State.HasFlag(ConsensusState.BlockSent)) return false;
            if (validator.ValidatorIndex == _context.MyIndex) return false;
            if (validator.Version != ConsensusContext.Version) return false;

            if (validator.ValidatorIndex >= _context.ValidatorCount)
            {
                _logger.LogWarning(
                    $"Rejected consensus payload from validator={validator.ValidatorIndex} " +
                    $"since validator index is not correct"
                );
                return false;
            }

            if (!validator.PrevHash.Equals(_context.PreviousBlockHash))
            {
                _logger.LogWarning(
                    $"Rejected consensus payload from validator={validator.ValidatorIndex} " +
                    $"because of prev hash mismatch"
                );
                return false;
            }

            if (_blockchainContext.CurrentBlockHeader.Header.Index + 1 < validator.BlockIndex)
            {
                _logger.LogWarning(
                    $"Cannot handle consensus payload from validator={validator.ValidatorIndex} " +
                    $"at height={validator.BlockIndex}, since " +
                    $"local height={_blockchainContext.CurrentBlockHeader.Header.Index}"
                );
                return false;
            }

            if (validator.ViewNumber != _context.ViewNumber && !isChangeView)
            {
                _logger.LogWarning(
                    $"Rejected consensus payload from validator={validator.ValidatorIndex} " +
                    $"because view does not match, my={_context.ViewNumber} theirs={validator.ViewNumber}"
                );
                return false;
            }

            return true;
        }

        private void SignAndBroadcast(ConsensusMessage payload)
        {
            _broadcaster.Broadcast(_messageFactory.ConsensusMessage(payload));
        }
    }
}