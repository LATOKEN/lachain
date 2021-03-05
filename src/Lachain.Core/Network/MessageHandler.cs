using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Consensus;
using Lachain.Logger;
using Lachain.Networking;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Utility.Utils;

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
            //Logger.LogTrace("Start processing PingReply");
            var (reply, publicKey) = @event;
            _blockSynchronizer.HandlePeerHasBlocks(reply.BlockHeight, publicKey);
            //Logger.LogTrace("Finished processing PingReply");
        }

        private void OnSyncBlocksRequest(object sender,
            (SyncBlocksRequest request, Action<SyncBlocksReply> callback) @event
        )
        {
            Logger.LogTrace("Start processing SyncBlocksRequest");
            var (request, callback) = @event;
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
                                        _stateManager.LastApprovedSnapshot.Transactions.GetTransactionByHash(txHash))
                            }
                        })
                }
            };
            callback(reply);
            Logger.LogTrace("Finished processing SyncBlocksRequest");
        }

        private void OnSyncBlocksReply(object sender, (SyncBlocksReply reply, ECDSAPublicKey publicKey) @event)
        {
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
            var (request, callback) = @event;
            Logger.LogTrace($"Get request for {request.Hashes.Count} transactions");
            var txs = request.Hashes
                .Select(txHash => _stateManager.LastApprovedSnapshot.Transactions.GetTransactionByHash(txHash) ??
                                  _transactionPool.GetByHash(txHash))
                .Where(tx => tx != null)
                .Select(tx => tx!)
                .ToList();
            Logger.LogTrace($"Replying request with {txs.Count} transactions");
            if (txs.Count == 0) return;
            callback(new SyncPoolReply {Transactions = {txs}});
        }

        private void OnSyncPoolReply(object sender, (SyncPoolReply reply, ECDSAPublicKey publicKey) @event)
        {
            Logger.LogTrace("Start processing SyncPoolReply");
            var (reply, publicKey) = @event;
            _blockSynchronizer.HandleTransactionsFromPeer(
                reply.Transactions ?? Enumerable.Empty<TransactionReceipt>(), publicKey
            );
            Logger.LogTrace("Finished processing SyncPoolReply");
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void OnConsensusMessage(object sender, (ConsensusMessage message, ECDSAPublicKey publicKey) @event)
        {
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