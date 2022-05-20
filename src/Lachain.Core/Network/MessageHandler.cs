using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Lachain.Logger;
using Lachain.Core.Blockchain.Checkpoints;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Blockchain.Operations;
using Lachain.Core.Consensus;
using Lachain.Core.Network.FastSync;
using Lachain.Networking;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Storage.Trie;
using Lachain.Storage.Repositories;
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
        private readonly ISnapshotIndexRepository _snapshotIndexer;
        private readonly IBlockManager _blockManager;
        private readonly INodeRetrieval _nodeRetrieval;
        private readonly ICheckpointManager _checkpointManager;
        private readonly INetworkManager _networkManager;
        private readonly IDownloader _downloader;
        
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
            INetworkManager networkManager,
            ISnapshotIndexRepository snapshotIndexer,
            INodeRetrieval nodeRetrieval,
            ICheckpointManager checkpointManager,
            IDownloader downloader
        )
        {
            _blockSynchronizer = blockSynchronizer;
            _transactionPool = transactionPool;
            _stateManager = stateManager;
            _consensusManager = consensusManager;
            _networkManager = networkManager;
            _blockManager = blockManager;
            _snapshotIndexer = snapshotIndexer;
            _nodeRetrieval = nodeRetrieval;
            _checkpointManager = checkpointManager;
            _downloader = downloader;
            blockManager.OnBlockPersisted += BlockManagerOnBlockPersisted;
            transactionPool.TransactionAdded += TransactionPoolOnTransactionAdded;
            _networkManager.OnPingReply += OnPingReply;
            _networkManager.OnSyncBlocksRequest += OnSyncBlocksRequest;
            _networkManager.OnSyncBlocksReply += OnSyncBlocksReply;
            _networkManager.OnSyncPoolRequest += OnSyncPoolRequest;
            _networkManager.OnSyncPoolReply += OnSyncPoolReply;
            _networkManager.OnConsensusMessage += OnConsensusMessage;
            _networkManager.OnRootHashByTrieNameRequest += OnRootHashByTrieNameRequest;
            _networkManager.OnRootHashByTrieNameReply += OnRootHashByTrieNameReply;
            _networkManager.OnBlockBatchRequest += OnBlockBatchRequest;
            _networkManager.OnBlockBatchReply += OnBlockBatchReply;
            _networkManager.OnTrieNodeByHashRequest += OnTrieNodeByHashRequest;
            _networkManager.OnTrieNodeByHashReply += OnTrieNodeByHashReply;
            _networkManager.OnCheckpointRequest += OnCheckpointRequest;
            _networkManager.OnCheckpointReply += OnCheckpointReply;
            _networkManager.OnCheckpointBlockRequest += OnCheckpointBlockRequest;
            _networkManager.OnCheckpointBlockReply += OnCheckpointBlockReply;
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
            if (request.ToHeight >= request.FromHeight)
            {
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
            }
            else
            {
                Logger.LogWarning($"Invalid height range in SyncBlockRequest: {request.FromHeight}-{request.ToHeight}");
                var reply = new SyncBlocksReply { Blocks={}};
                callback(reply);
            }

            Logger.LogTrace("Finished processing SyncBlocksRequest");
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
            using var timer = IncomingMessageHandlingTime.WithLabels("SyncPoolReply").NewTimer();
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

        private void OnRootHashByTrieNameReply(object sender, (RootHashByTrieNameReply reply, ECDSAPublicKey publicKey) @event)
        {
            using var timer = IncomingMessageHandlingTime.WithLabels("OnRootHashByTrieNameReply").NewTimer();
            Logger.LogTrace("Start processing OnRootHashByTrieNameReply");
            var (reply, publicKey) = @event;
            Task.Factory.StartNew(() =>
            {
                try
                {
                    var rootHash = reply.RootHash;
                    _downloader.HandleCheckpointStateHashFromPeer(rootHash, reply.RequestId, publicKey);
                }
                catch (Exception exception)
                {
                    Logger.LogError($"Error occured while handling root hash from peer {publicKey.ToHex()}: {exception}");
                }
            }, TaskCreationOptions.LongRunning);
            Logger.LogTrace("Finished processing OnRootHashByTrieNameReply");
        }

        private void OnRootHashByTrieNameRequest(object sender,
            (RootHashByTrieNameRequest request, Action<RootHashByTrieNameReply> callback) @event
        )
        {
            using var timer = IncomingMessageHandlingTime.WithLabels("OnRootHashByTrieNameRequest").NewTimer();
            Logger.LogTrace("Start processing OnRootHashByTrieNameRequest");
            var (request, callback) = @event;
            try
            {
                var blockchainSnapshot = _snapshotIndexer.GetSnapshotForBlock(request.Block);
                var snapshot = blockchainSnapshot.GetSnapshot(request.TrieName);
                var reply = new RootHashByTrieNameReply
                {
                    RootHash = (snapshot is null) ? UInt256Utils.Zero : snapshot.Hash,
                    RequestId = request.RequestId
                };
                callback(reply);
            }
            catch (Exception exception)
            {
                Logger.LogWarning($"Got exception trying to get root hash for trie {request.TrieName}"
                    + $" for block {request.Block} : {exception}");
                var reply = new RootHashByTrieNameReply
                {
                    RootHash = UInt256Utils.Zero,
                    RequestId = request.RequestId
                };
                callback(reply);
            }

            Logger.LogTrace("Finished processing OnRootHashByTrieNameRequest");
        }

        private void OnBlockBatchReply(object? sender, (BlockBatchReply reply, ECDSAPublicKey publicKey) @event)
        {
            using var timer = IncomingMessageHandlingTime.WithLabels("OnBlockBatchReply").NewTimer();
            Logger.LogTrace("Start processing OnBlockBatchReply");
            var (reply, publicKey) = @event;
            Task.Factory.StartNew(() =>
            {
                try
                {
                    var blocks = reply.BlockBatch.ToList();
                    var requestId = reply.RequestId;
                    if (blocks is null) blocks = new List<Block>();
                    _downloader.HandleBlocksFromPeer(blocks, requestId, publicKey);
                }
                catch (Exception exception)
                {
                    Logger.LogError($"Error occured while handling blocks from peer {publicKey.ToHex()}: {exception}");
                }
            }, TaskCreationOptions.LongRunning);
            Logger.LogTrace("Finished processing OnBlockBatchReply");
        }

        private void OnBlockBatchRequest(object? sender,
            (BlockBatchRequest request, Action<BlockBatchReply> callback) @event
        )
        {
            using var timer = IncomingMessageHandlingTime.WithLabels("OnBlockBatchRequest").NewTimer();
            Logger.LogTrace("Start processing OnBlockBatchRequest");
            var (request, callback) = @event;
            try
            {
                var fromBlock = request.FromHeight;
                var toBlock = request.ToHeight;
                List<Block> blockBatch = new List<Block>();
                if (toBlock > _blockManager.GetHeight())
                {
                    Logger.LogWarning($"I don't have all blocks. Requested max block: {toBlock}"
                        + $" and my height: {_blockManager.GetHeight()}. So chose not to reply.");
                }
                else if(fromBlock <= toBlock)
                {
                    for (var blockId = fromBlock; blockId <= toBlock; blockId++)
                    {
                        var block = _blockManager.GetByHeight(blockId);
                        if (block == null)
                        {
                            Logger.LogWarning($"Found null block for {blockId} which should not happen. My height: "
                                + $"{_blockManager.GetHeight()}, max block number requested: {toBlock}. So chose not to reply.");
                            blockBatch.Clear();
                            break;
                        }
                        blockBatch.Add(block);
                    }
                }
                var reply = new BlockBatchReply
                {
                    BlockBatch = {blockBatch},
                    RequestId = request.RequestId
                };
                callback(reply);
            }
            catch (Exception exception)
            {
                Logger.LogWarning($"Got exception trying to get blocks : {exception}");
                var reply = new BlockBatchReply
                {
                    BlockBatch = {new List<Block>()},
                    RequestId = request.RequestId
                };
                callback(reply);
            }

            Logger.LogTrace("Finished processing OnBlockBatchRequest");
        }

        private void OnTrieNodeByHashReply(object? sender, (TrieNodeByHashReply reply, ECDSAPublicKey publicKey) @event)
        {
            using var timer = IncomingMessageHandlingTime.WithLabels("OnTrieNodeByHashReply").NewTimer();
            Logger.LogTrace("Start processing OnTrieNodeByHashReply");
            var (reply, publicKey) = @event;
            Task.Factory.StartNew(() =>
            {
                try
                {
                    var trieNodes = reply.TrieNodes.ToList();
                    if (trieNodes is null) trieNodes = new List<TrieNodeInfo>();
                    var requestId = reply.RequestId;
                    _downloader.HandleNodesFromPeer(trieNodes, requestId, publicKey);
                }
                catch (Exception exception)
                {
                    Logger.LogError($"Error occured while handling trie nodes from peer {publicKey.ToHex()}: {exception}");
                }
            }, TaskCreationOptions.LongRunning);
            Logger.LogTrace("Finished processing OnTrieNodeByHashReply");
        }

        private void OnTrieNodeByHashRequest(object? sender, 
            (TrieNodeByHashRequest request, Action<TrieNodeByHashReply> callback) @event
        )
        {
            using var timer = IncomingMessageHandlingTime.WithLabels("OnTrieNodeByHashRequest").NewTimer();
            Logger.LogTrace("Start processing OnTrieNodeByHashRequest");
            var (request, callback) = @event;
            
            try
            {
                var nodeHashes = request.NodeHashes.ToList();
                var trieNodeInfoList = new List<TrieNodeInfo>();
                foreach (var nodeHash in nodeHashes)
                {
                    IHashTrieNode? node = _nodeRetrieval.TryGetNode(nodeHash.ToBytes(), out var childrenHash);
                    if (node is null)
                    {
                        Logger.LogWarning($"Found null node for hash: {nodeHash.ToHex()}. So chose not to reply.");
                        trieNodeInfoList.Clear();
                        break;
                    }
                    var nodeInfo = new TrieNodeInfo();

                    switch (node)
                    {
                        case InternalNode internalNode:
                            var childrenHashes = new List<UInt256>();
                            foreach(var childHash in childrenHash) childrenHashes.Add(childHash.ToUInt256());
                            nodeInfo.InternalNodeInfo = new InternalNodeInfo
                            {
                                NodeType = ByteString.CopyFrom((byte) internalNode.Type),
                                Hash = internalNode.Hash.ToUInt256(),
                                ChildrenMask = internalNode.ChildrenMask,
                                ChildrenHash = { childrenHashes }
                            };
                            break;

                        case LeafNode leafNode:
                            nodeInfo.LeafNodeInfo = new LeafNodeInfo
                            {
                                NodeType = ByteString.CopyFrom((byte) leafNode.Type),
                                Hash = leafNode.Hash.ToUInt256(),
                                KeyHash = leafNode.KeyHash.ToUInt256(),
                                Value = ByteString.CopyFrom(leafNode.Value)
                            };
                            break;
                    }
                    trieNodeInfoList.Add(nodeInfo);
                }

                var reply = new TrieNodeByHashReply
                {
                    TrieNodes = {trieNodeInfoList},
                    RequestId = request.RequestId
                };
                callback(reply);
            }
            catch (Exception exception)
            {
                Logger.LogWarning($"Got exception trying to get trie nodes: {exception}");
                var reply = new TrieNodeByHashReply
                {
                    TrieNodes = {new List<TrieNodeInfo>()},
                    RequestId = request.RequestId
                };
                callback(reply);
            }
            
            Logger.LogTrace("Finished processing OnTrieNodeByHashRequest");
        }

        private void OnCheckpointRequest(object? sender, 
            (CheckpointRequest request, Action<CheckpointReply> callback) @event)
        {
            using var timer = IncomingMessageHandlingTime.WithLabels("OnCheckpointRequest").NewTimer();
            Logger.LogTrace("Start processing OnCheckpointRequest");
            var (request, callback) = @event;
            try
            {
                var checkpointTypes = request.CheckpointType.ToByteArray();
                var checkpoints = new List<CheckpointInfo>();
                foreach (var checkpointType in checkpointTypes)
                {
                    checkpoints.Add(_checkpointManager.GetCheckpointInfo((CheckpointType) checkpointType));
                }
                var reply = new CheckpointReply
                {
                    Checkpoints = { checkpoints },
                    RequestId = request.RequestId
                };
                callback(reply);
            }
            catch (Exception exception)
            {
                Logger.LogWarning($"Got exception trying to get checkpoints: {exception}");
                var reply = new CheckpointReply
                {
                    Checkpoints = { new List<CheckpointInfo>() },
                    RequestId = request.RequestId
                };
                callback(reply);
            }
            Logger.LogTrace("Finished processing OnCheckpointRequest");
        }

        private void OnCheckpointReply(object? sender, (CheckpointReply reply, ECDSAPublicKey publicKey) @event)
        {
            using var timer = IncomingMessageHandlingTime.WithLabels("OnCheckpointReply").NewTimer();
            Logger.LogTrace("Start processing OnCheckpointReply");
            var (reply, publicKey) = @event;
            Task.Factory.StartNew(() =>
            {
                try
                {
                    var checkpoints = reply.Checkpoints.ToList();
                    if (checkpoints is null) checkpoints = new List<CheckpointInfo>();
                    _blockSynchronizer.HandleCheckpointFromPeer(checkpoints, publicKey, reply.RequestId);
                }
                catch (Exception exception)
                {
                    Logger.LogError($"Error occured while handling checkpoints from peer {publicKey.ToHex()}: {exception}");
                }
            }, TaskCreationOptions.LongRunning);
            Logger.LogTrace("Finished processing OnCheckpointReply");
        }

        private void OnCheckpointBlockRequest(object? sender, 
            (CheckpointBlockRequest request, Action<CheckpointBlockReply> callback) @event)
        {
            using var timer = IncomingMessageHandlingTime.WithLabels("OnCheckpointBlockRequest").NewTimer();
            Logger.LogTrace("Start processing OnCheckpointBlockRequest");
            var (request, callback) = @event;
            var blockHeight = request.BlockHeight;
            var block = _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(blockHeight);
            var reply = new CheckpointBlockReply
            {
                Block = block,
                RequestId = request.RequestId
            };
            callback(reply);
            Logger.LogTrace("Finished processing OnCheckpointBlockRequest");
        }

        private void OnCheckpointBlockReply(object? sender, (CheckpointBlockReply reply, ECDSAPublicKey publicKey) @event)
        {
            using var timer = IncomingMessageHandlingTime.WithLabels("OnCheckpointBlockReply").NewTimer();
            Logger.LogTrace("Start processing OnCheckpointBlockReply");
            var (reply, publicKey) = @event;
            Task.Factory.StartNew(() =>
            {
                try
                {
                    var block = reply.Block;
                    _downloader.HandleCheckpointBlockFromPeer(block, reply.RequestId, publicKey);
                }
                catch (Exception exception)
                {
                    Logger.LogError($"Error occured while handling checkpoints from peer {publicKey.ToHex()}: {exception}");
                }
            }, TaskCreationOptions.LongRunning);
            Logger.LogTrace("Finished processing OnCheckpointBlockReply");
        }

    }
}