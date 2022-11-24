using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Google.Protobuf.Collections;
using Lachain.Logger;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Consensus;
using Lachain.Networking;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Utility.Utils;
using Prometheus;

namespace Lachain.Core.Network
{
    public class MessageHandler : IMessageHandler
    {
        private static readonly ILogger<MessageHandler> Logger = LoggerFactory.GetLoggerForClass<MessageHandler>();

        private readonly IBlockSynchronizer _blockSynchronizer;
        private readonly ITransactionPool _transactionPool;
        private readonly IStateManager _stateManager;
        private readonly IConsensusManager _consensusManager;

        private readonly INetworkManager _networkManager;
        
        private static readonly Summary IncomingMessageHandlingTime = Metrics.CreateSummary(
            "lachain_message_handling_duration_seconds",
            "Duration of incoming message handler execution for last 5 minutes",
            new SummaryConfiguration
            {
                MaxAge = TimeSpan.FromMinutes(5),
                LabelNames = new []{"message_type"},
                Objectives = new[]
                {
                    new QuantileEpsilonPair(0.95, 0.05),
                    new QuantileEpsilonPair(0.5, 0.05)
                }
            }
        );

        /*
         * TODO: message queue is a hack. We should design additional layer for storing/persisting consensus messages
         */
        private readonly IDictionary<long, List<Tuple<ConsensusMessage, ECDSAPublicKey>>> _queuedMessages =
            new ConcurrentDictionary<long, List<Tuple<ConsensusMessage, ECDSAPublicKey>>>();

        public MessageHandler(
            IBlockSynchronizer blockSynchronizer,
            ITransactionPool transactionPool,
            IStateManager stateManager,
            IConsensusManager consensusManager,
            IBlockManager blockManager,
            INetworkManager networkManager
        )
        {
            _blockSynchronizer = blockSynchronizer;
            _transactionPool = transactionPool;
            _stateManager = stateManager;
            _consensusManager = consensusManager;
            _networkManager = networkManager;
            blockManager.OnBlockPersisted += BlockManagerOnBlockPersisted;
            transactionPool.TransactionAdded += TransactionPoolOnTransactionAdded;
            _networkManager.OnPingReply += OnPingReply;
            _networkManager.OnSyncBlocksRequest += OnSyncBlocksRequest;
            _networkManager.OnSyncBlocksReply += OnSyncBlocksReply;
            _networkManager.OnSyncPoolRequest += OnSyncPoolRequest;
            _networkManager.OnSyncPoolReply += OnSyncPoolReply;
            _networkManager.OnConsensusMessage += OnConsensusMessage;
        }

        private void TransactionPoolOnTransactionAdded(object sender, TransactionReceipt e)
        {
            _networkManager.BroadcastLocalTransaction(e);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void BlockManagerOnBlockPersisted(object sender, Block e)
        {
            var era = (long) e.Header.Index + 1;
            if (!_queuedMessages.TryGetValue(era, out var messages)) return;
            _queuedMessages.Remove(era);
            foreach (var (message, key) in messages)
            {
                OnConsensusMessage(this, (message, key));
            }
        }

        private void OnPingReply(object sender, (PingReply reply, ECDSAPublicKey publicKey) @event)
        {
            using var timer = IncomingMessageHandlingTime.WithLabels("PingReply").NewTimer();
            var (reply, publicKey) = @event;
            _blockSynchronizer.HandlePeerHasBlocks(reply.BlockHeight, publicKey);
        }

        private void OnSyncBlocksRequest(object sender,
            (SyncBlocksRequest request, Action<SyncBlocksReply> callback) @event
        )
        {
            
            using var timer = IncomingMessageHandlingTime.WithLabels("SyncBlocksRequest").NewTimer();
            Logger.LogTrace("Start processing SyncBlocksRequest");
            var (request, callback) = @event;

            try
            {
                ValidateSyncBlocksRequest(request,  _stateManager.LastApprovedSnapshot.Blocks);
            }
            catch (ArgumentException e)
            {
                Logger.LogWarning("Invalid sync block request: " + e.Message);
                var emptyReply = new SyncBlocksReply { Blocks={}};
                callback(emptyReply);
                return;
            }
            
            var reply = new SyncBlocksReply
            {
                Blocks =
                {
                    _stateManager.LastApprovedSnapshot.Blocks
                        .GetBlocksByHeightRange(request.FromHeight, request.ToHeight - request.FromHeight + 1)
                        .Select(block => new BlockInfo
                        {
                            Block = block,
                            Transactions =
                            {
                                block.TransactionHashes
                                    .Select(txHash =>
                                        _stateManager.LastApprovedSnapshot.Transactions
                                            .GetTransactionByHash(txHash)?? new TransactionReceipt())
                            }
                        })
                }
            };
            callback(reply);
            
            Logger.LogTrace("Finished processing SyncBlocksRequest");
        }

        //To do: put this constant in a better place, and document block init rule
        //To do: encode error in reply
        private const ulong SyncRequestBlockLimit = 100;
        private void ValidateSyncBlocksRequest(SyncBlocksRequest request, IBlockSnapshot snapshot)
        {
            if (request.FromHeight == null)
            {
                throw new ArgumentException("From Height Cannot be null");
            }
            
            if (request.ToHeight == null)
            {
                throw new ArgumentException("To Height Cannot be null");
            }

            if (request.FromHeight < 0 || request.ToHeight < 0 || request.FromHeight > request.ToHeight)
            {
                throw new ArgumentException(
                    $"Invalid height range in SyncBlockRequest: {request.FromHeight}-{request.ToHeight}");
            }

            if (request.FromHeight > snapshot.GetTotalBlockHeight() ||
                request.ToHeight > snapshot.GetTotalBlockHeight())
            {
                throw new ArgumentException(
                    $"Height range ({request.FromHeight}-{request.ToHeight}) " +
                    $"greater than current block height {snapshot.GetTotalBlockHeight()}");
            }

            var count = request.ToHeight - request.FromHeight + 1;
            if (count > SyncRequestBlockLimit)
            {
                throw new ArgumentException(
                    $"Height range ({request.FromHeight}-{request.ToHeight}) " +
                    $"Has too many blocks {count}.");
            }
        }
        

        private void OnSyncBlocksReply(object sender, (SyncBlocksReply reply, ECDSAPublicKey publicKey) @event)
        {
            using var timer = IncomingMessageHandlingTime.WithLabels("SyncBlocksReply").NewTimer();
            Logger.LogTrace("Start processing SyncBlocksReply");
            var (reply, publicKey) = @event;
            var len = reply.Blocks?.Count ?? 0;
            var orderedBlocks = (reply.Blocks ?? Enumerable.Empty<BlockInfo>())
                .Where(x => x.Block?.Header?.Index != null)
                .OrderBy(x => x.Block.Header.Index)
                .ToArray();
            Logger.LogTrace($"Blocks received: {orderedBlocks.Length} ({len})");
            Task.Factory.StartNew(() =>
            {
                try
                {
                    foreach (var block in orderedBlocks)
                    {
                        if (!_blockSynchronizer.HandleBlockFromPeer(block, publicKey))
                            break;
                    }
                }
                catch (Exception e)
                {
                    Logger.LogError($"Error occured while handling blocks from peer: {e}");
                }
            }, TaskCreationOptions.LongRunning);
            Logger.LogTrace("Finished processing SyncBlocksReply");
        }
        
        private void OnSyncPoolRequest(object sender,
            (SyncPoolRequest request, Action<SyncPoolReply> callback) @event)
        {
            
            using var timer = IncomingMessageHandlingTime.WithLabels("SyncPoolRequest").NewTimer();
            var (request, callback) = @event;
            Logger.LogTrace($"Get request for {request.Hashes.Count} transactions");
            
            try
            {
                ValidateSyncPoolRequest(request);
            }
            catch (ArgumentException e)
            {
                Logger.LogWarning("Invalid sync pool request: " + e.Message);
                var emptyReply = new SyncPoolReply() { Transactions={}};
                callback(emptyReply);
                return;
            }

            
            List<TransactionReceipt> txs;
            if (request.All)
            {
                txs = _transactionPool.Transactions.Values.ToList();
            }
            else
            {
                txs = request.Hashes
                    .Select(txHash => _stateManager.LastApprovedSnapshot.Transactions.GetTransactionByHash(txHash) ??
                                      _transactionPool.GetByHash(txHash))
                    .Where(tx => tx != null)
                    .Select(tx => tx!)
                    .ToList();
            }

            Logger.LogTrace($"Replying request with {txs.Count} transactions");
            if (txs.Count == 0) return;
            callback(new SyncPoolReply {Transactions = {txs}});
        }
        
        //To do: put this constant in a better place, and document block init rule
        //To do: encode error in reply
        private const int PoolSyncRequestTransactionLimit = 1000;
        private void ValidateSyncPoolRequest(SyncPoolRequest request)
        {
            if (request.All)
            {
                if (!(request.Hashes is null))
                {
                    throw new ArgumentException("Pool request has both all switch and list of txns.");
                }
            }

            if (request.Hashes is null || request.Hashes.Count == 0)
            {
                throw new ArgumentException("No hashes provided in request");
            }

            if (PoolSyncRequestTransactionLimit > 0 && request.Hashes.Count > PoolSyncRequestTransactionLimit)
            {
                throw new ArgumentException("Too many hashes in request");
            }
            
        }

        private void OnSyncPoolReply(object sender, (SyncPoolReply reply, ECDSAPublicKey publicKey) @event)
        {
            using var timer = IncomingMessageHandlingTime.WithLabels("SyncPoolReply").NewTimer();
            Logger.LogTrace("Start processing SyncPoolReply");
            
            
            var (reply, publicKey) = @event;
            try
            {
                ValidateSyncPoolReply(reply);
            }
            catch (ArgumentException e)
            {
                Logger.LogWarning("Invalid sync pool reply: " + e.Message);
                return;
            }
            
            _blockSynchronizer.HandleTransactionsFromPeer(
                reply.Transactions ?? Enumerable.Empty<TransactionReceipt>(), publicKey
            );
            Logger.LogTrace("Finished processing SyncPoolReply");
        }

        private void ValidateSyncPoolReply(SyncPoolReply syncPoolReply)
        {
            if (syncPoolReply.Transactions is null)
            {
                throw new ArgumentException("Transaction list is null");
            }

            if (PoolSyncRequestTransactionLimit > 0 && syncPoolReply.Transactions.Count > PoolSyncRequestTransactionLimit)
            {
                throw new ArgumentException("Too many transactions in request");
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void OnConsensusMessage(object sender, (ConsensusMessage message, ECDSAPublicKey publicKey) @event)
        {
            using var timer = IncomingMessageHandlingTime.WithLabels("ConsensusMessage").NewTimer();
            var (message, publicKey) = @event;
            try
            {
                _consensusManager.Dispatch(message, publicKey);
            }
            catch (ConsensusStateNotPresentException)
            {
                _queuedMessages.ComputeIfAbsent(
                        message.Validator.Era,
                        x => new List<Tuple<ConsensusMessage, ECDSAPublicKey>>()
                    )
                    .Add(new Tuple<ConsensusMessage, ECDSAPublicKey>(message, publicKey));

                Logger.LogTrace("Queued message too far in future...");
            }
        }
    }
}