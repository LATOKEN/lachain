using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Google.Protobuf;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Operations;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Consensus;
using Lachain.Core.Network.FastSynchronizerBatch;
using Lachain.Logger;
using Lachain.Networking;
using Lachain.Proto;
using Lachain.Storage.Repositories;
using Lachain.Storage.State;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using NLog;

namespace Lachain.Core.Network
{
    public class BlockSynchronizer : IBlockSynchronizer
    {
        private static readonly ILogger<BlockSynchronizer>
            Logger = LoggerFactory.GetLoggerForClass<BlockSynchronizer>();

        private readonly ITransactionManager _transactionManager;
        private readonly IBlockManager _blockManager;
        private readonly INetworkBroadcaster _networkBroadcaster;
        private readonly INetworkManager _networkManager;
        private readonly ITransactionPool _transactionPool;
        private readonly IStateManager _stateManager;
        private readonly IFastSynchronizerBatch _fastSync;

        private readonly object _peerHasTransactions = new object();
        private readonly object _peerHasBlocks = new object();
        private readonly object _blocksLock = new object();
        private readonly object _txLock = new object();
        private readonly object _peerHashCheckpoint = new object();
        private LogLevel _logLevelForSync = LogLevel.Trace;
        private bool _running;
        private readonly Thread _blockSyncThread;
        private readonly Thread _pingThread;

        private readonly IDictionary<ECDSAPublicKey, ulong> _peerHeights
            = new ConcurrentDictionary<ECDSAPublicKey, ulong>();

        private IDictionary<UInt256, TransactionReceipt?> _requiredTxs = new ConcurrentDictionary<UInt256, TransactionReceipt?>();
        // These are checkpoint info
        // We will get these info from other peers via network messaging.
        // But we cannot start fast sync whenever we get these info because then any peer can start fastsync
        // by sending valid checkpoint informations from outside. We don't want that.
        // So we need to control when to start fastsync and some logic to for that.
        private bool? _checkpointExist;
        private ulong? _checkpointBlockHeight;
        private UInt256? _checkpointBlockHash;
        private List<(UInt256, CheckpointType)>? _stateHashes;
        private ulong? _checkpointRequesId;

        public BlockSynchronizer(
            ITransactionManager transactionManager,
            IBlockManager blockManager,
            INetworkBroadcaster networkBroadcaster,
            INetworkManager networkManager,
            ITransactionPool transactionPool,
            IStateManager stateManager,
            IFastSynchronizerBatch fastSync
        )
        {
            _transactionManager = transactionManager;
            _blockManager = blockManager;
            _networkBroadcaster = networkBroadcaster;
            _networkManager = networkManager;
            _transactionPool = transactionPool;
            _stateManager = stateManager;
            _fastSync = fastSync;
            _blockSyncThread = new Thread(BlockSyncWorker);
            _pingThread = new Thread(PingWorker);
        }

        public event EventHandler<ulong>? OnSignedBlockReceived;

        public uint WaitForTransactions(IEnumerable<UInt256> transactionHashes, TimeSpan timeout, out List<TransactionReceipt> receipts)
        {
            receipts = new List<TransactionReceipt>();
            var txHashes = transactionHashes as UInt256[] ?? transactionHashes.ToArray();
            foreach(var txHash in txHashes)
                _requiredTxs[txHash] = null;
            
            var lostTxs = _GetMissingTransactions(txHashes);

            if(lostTxs.Count == 0)
            {
                foreach(var txHash in txHashes)
                {
                    if(_requiredTxs[txHash] is null) 
                        throw new Exception("Did not get the transaction receipt from peers");
                    receipts.Add(_requiredTxs[txHash]!);
                }
                _requiredTxs.Clear();
                return (uint) txHashes.Length;
            }

            var endWait = DateTime.UtcNow.Add(timeout);

            while (_GetMissingTransactions(txHashes).Count != 0)
            {
                const int maxPeersToAsk = 1;
                var maxHeight = _peerHeights.Values.Count == 0 ? 0 : _peerHeights.Values.Max();
                var rnd = new Random();
                var peers = _peerHeights
                    .Where(entry => entry.Value >= maxHeight)
                    .Select(entry => entry.Key)
                    .OrderBy(_ => rnd.Next())
                    .Take(maxPeersToAsk)
                    .ToArray();

                Logger.LogTrace($"Sending query for {lostTxs.Count} transactions to {peers.Length} peers");
                var request = _networkManager.MessageFactory.SyncPoolRequest(lostTxs);
                foreach (var peer in peers) _networkManager.SendTo(peer, request);
                lock (_peerHasTransactions)
                    Monitor.Wait(_peerHasTransactions, TimeSpan.FromMilliseconds(5_000));
                if (DateTime.UtcNow.CompareTo(endWait) > 0) break;
            }

            foreach(var txHash in txHashes)
            {
                if(_requiredTxs[txHash] is null) 
                    throw new Exception("Timed out. Yet did not get the transaction receipt from peers");
                receipts.Add(_requiredTxs[txHash]!);
            }
            _requiredTxs.Clear();
            return (uint) txHashes.Length;
        }

        public uint HandleTransactionsFromPeer(IEnumerable<TransactionReceipt> transactions, ECDSAPublicKey publicKey)
        {
            lock (_txLock)
            {
                var txs = transactions.ToArray();
                Logger.LogTrace($"Received {txs.Length} transactions from peer {publicKey.ToHex()}");
                var persisted = 0u;
                foreach (var tx in txs)
                {
                    if(_requiredTxs.Contains(new KeyValuePair<UInt256, TransactionReceipt?>(tx.Hash, null)))
                    {
                        Logger.LogTrace($"Added {tx.Hash.ToHex()} to the requiredTxs");
                        _requiredTxs[tx.Hash] = tx;
                    }
                    if (tx.Signature.IsZero())
                    {
                        Logger.LogTrace($"Received zero-signature transaction: {tx.Hash.ToHex()}");
                        if (_transactionPool.Add(tx, false) == OperatingError.Ok)
                            persisted++;
                        continue;
                    }

                    var error = _transactionManager.Verify(tx);
                    if (error != OperatingError.Ok)
                    {
                        Logger.LogTrace($"Unable to verify transaction: {tx.Hash.ToHex()} ({error})");
                        continue;
                    }

                    error = _transactionPool.Add(tx, false);
                    if (error == OperatingError.Ok)
                        persisted++;
                    else
                        Logger.LogTrace($"Transaction {tx.Hash.ToHex()} not persisted: {error}");
                }

                lock (_peerHasTransactions)
                    Monitor.PulseAll(_peerHasTransactions);
                Logger.LogTrace($"Persisted {persisted} transactions from peer {publicKey.ToHex()}");
                return persisted;
            }
        }

        public bool HandleBlockFromPeer(BlockInfo blockWithTransactions, ECDSAPublicKey publicKey)
        {
            Logger.LogTrace("HandleBlockFromPeer");
            lock (_blocksLock)
            {
                var block = blockWithTransactions.Block;
                var receipts = blockWithTransactions.Transactions;
                Logger.LogDebug(
                    $"Got block {block.Header.Index} with hash {block.Hash.ToHex()} from peer {publicKey.ToHex()}");
                var myHeight = _blockManager.GetHeight();
                if (block.Header.Index != myHeight + 1)
                {
                    Logger.LogTrace(
                        $"Skipped block {block.Header.Index} from peer {publicKey.ToHex()}: our height is {myHeight}");
                    return false;
                }

                if (!block.TransactionHashes.ToHashSet().SetEquals(receipts.Select(r => r.Hash)))
                {
                    try
                    {
                        var needHashes = string.Join(", ", block.TransactionHashes.Select(x => x.ToHex()));
                        var gotHashes = string.Join(", ", receipts.Select(x => x.Hash.ToHex()));

                        Logger.LogTrace(
                            $"Skipped block {block.Header.Index} from peer {publicKey.ToHex()}: expected hashes [{needHashes}] got hashes [{gotHashes}]");
                    }
                    catch (Exception e)
                    {
                        Logger.LogWarning($"Failed to get transaction receipts for tx hash: {e}");
                    }

                    return false;
                }

                if (_blockManager.VerifySignatures(block, true) != OperatingError.Ok)
                {
                    Logger.LogTrace($"Skipped block {block.Header.Index} from peer {publicKey.ToHex()}: invalid multisig");
                    return false;
                }
                // This is to tell consensus manager to terminate current era, since we trust given multisig
                OnSignedBlockReceived?.Invoke(this, block.Header.Index);

                var error = _stateManager.SafeContext(() =>
                {
                    if (_blockManager.GetHeight() + 1 == block.Header.Index)
                        return _blockManager.Execute(block, receipts, commit: true, checkStateHash: true);
                    Logger.LogTrace(
                        $"We have blockchain with height {_blockManager.GetHeight()} but got block {block.Header.Index}");
                    return OperatingError.BlockAlreadyExists;
                });
                if (error == OperatingError.BlockAlreadyExists)
                {
                    Logger.LogTrace(
                        $"Skipped block {block.Header.Index} from peer {publicKey.ToHex()}: block already exists");
                    return true;
                }

                if (error != OperatingError.Ok)
                {
                    Logger.LogWarning(
                        $"Unable to persist block {block.Header.Index} (current height {_blockManager.GetHeight()}), got error {error}, dropping peer");
                    return false;
                }

                lock (_peerHasBlocks)
                    Monitor.PulseAll(_peerHasBlocks);
                return true;
            }
        }

        public void HandlePeerHasBlocks(ulong blockHeight, ECDSAPublicKey publicKey)
        {
            Logger.Log(_logLevelForSync, $"Peer {publicKey.ToHex()} has height {blockHeight}");
            lock (_peerHasBlocks)
            {
                if (_peerHeights.TryGetValue(publicKey, out var peerHeight) && blockHeight <= peerHeight)
                    return;
                _peerHeights[publicKey] = blockHeight;
                _fastSync.AddPeer(publicKey);
                Monitor.PulseAll(_peerHasBlocks);
            }
        }

        public bool IsSynchronizingWith(IEnumerable<ECDSAPublicKey> peers)
        {
            var myHeight = _blockManager.GetHeight();
            if (myHeight > _networkManager.LocalNode.BlockHeight)
                _networkManager.LocalNode.BlockHeight = myHeight;
            var setOfPeers = peers.ToHashSet();
            if (setOfPeers.Count == 0) return false;

            lock (_peerHasBlocks)
                Monitor.Wait(_peerHasBlocks, TimeSpan.FromSeconds(1));
            var validatorPeers = _peerHeights
                .Where(entry => setOfPeers.Contains(entry.Key))
                .ToArray();
            Logger.LogDebug($"Got {validatorPeers.Length} connected out of {setOfPeers.Count}");
            if (validatorPeers.Length < setOfPeers.Count * 2 / 3)
                return true;
            if (!validatorPeers.Any()) return false;

            List<ulong> heights = new List<ulong>();
            foreach (var keyValuePair in validatorPeers)
            {
                heights.Add(keyValuePair.Value);
            }
            heights.Sort();
            var medianHeight = heights[heights.Count / 2];
            Logger.LogDebug($"Median height among peers: {medianHeight}, my height: {myHeight}");
            return myHeight < medianHeight;
        }

        public void SynchronizeWith(IEnumerable<ECDSAPublicKey> peers)
        {
            var peersArray = peers.ToArray();
            _logLevelForSync = LogLevel.Debug;
            Logger.LogDebug($"Synchronizing with peers: {string.Join(", ", peersArray.Select(x => x.ToHex()))}");
            while (IsSynchronizingWith(peersArray))
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(1_000));
            }

            _logLevelForSync = LogLevel.Trace;
        }

        public ulong? GetHighestBlock()
        {
            if (_peerHeights.Count == 0) return null;
            return _peerHeights.Max(v => v.Value);
        }

        public IDictionary<ECDSAPublicKey, ulong> GetConnectedPeers()
        {
            return _peerHeights;
        }

        private void PingWorker()
        {
            while (_running)
            {
                try
                {
                    var reply = _networkManager.MessageFactory.PingReply(
                        TimeUtils.CurrentTimeMillis(), _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight()
                    );
                    _networkBroadcaster.Broadcast(reply);
                    Logger.LogTrace($"Broadcasted our height: {reply.PingReply.BlockHeight}");
                }
                catch (Exception e)
                {
                    Logger.LogError($"Error in ping worker: {e}");
                }

                Thread.Sleep(TimeSpan.FromMilliseconds(3_000));
            }
        }

        private void BlockSyncWorker()
        {
            var rnd = new Random();
            Logger.LogDebug("Starting block synchronization worker");
            while (_running)
            {
                try
                {
                    var myHeight = _blockManager.GetHeight();
                    if (myHeight > _networkManager.LocalNode.BlockHeight)
                        _networkManager.LocalNode.BlockHeight = myHeight;

                    if (_peerHeights.Count == 0)
                    {
                        Logger.LogWarning("Peer height map is empty, nobody responds to pings?");
                        Thread.Sleep(TimeSpan.FromMilliseconds(1_000));
                        continue;
                    }

                    var maxHeight = _peerHeights.Values.Max();
                    if (myHeight >= maxHeight)
                    {
                        Logger.LogTrace($"Nothing to do: my height is {myHeight} and peers are at {maxHeight}");
                        Thread.Sleep(TimeSpan.FromMilliseconds(1_000));
                        continue;
                    }

                    const int maxPeersToAsk = 1;
                    const int maxBlocksToRequest = 10;

                    var peers = _peerHeights
                        .Where(entry => entry.Value >= maxHeight)
                        .Select(entry => entry.Key)
                        .OrderBy(_ => rnd.Next())
                        .Take(maxPeersToAsk)
                        .ToArray();

                    var leftBound = myHeight + 1;
                    var rightBound = Math.Min(maxHeight, myHeight + maxBlocksToRequest);
                    Logger.LogTrace($"Sending query for blocks [{leftBound}; {rightBound}] to {peers.Length} peers");
                    foreach (var peer in peers)
                    {
                        _networkManager.SendTo(
                            peer, _networkManager.MessageFactory.SyncBlocksRequest(leftBound, rightBound)
                        );
                    }

                    var waitStart = TimeUtils.CurrentTimeMillis();
                    while (true)
                    {
                        lock (_peerHasBlocks)
                        {
                            Monitor.Wait(_peerHasBlocks, TimeSpan.FromMilliseconds(1_000));
                        }

                        if (TimeUtils.CurrentTimeMillis() - waitStart > 5_000) break;
                    }
                }
                catch (Exception e)
                {
                    Logger.LogError($"Error in block synchronizer: {e}");
                    Thread.Sleep(1_000);
                }
            }
        }

        public void Start()
        {
            _running = true;
            _pingThread.Start();
            StartFastSync();
            _blockSyncThread.Start();
        }

        private List<UInt256> _GetMissingTransactions(IEnumerable<UInt256> txHashes)
        {
            List<UInt256> missingTransactions = new List<UInt256>();
            foreach(var txHash in txHashes)
            {
                if(_requiredTxs[txHash] is null)
                {
                    _requiredTxs[txHash] = _transactionManager.GetByHash(txHash) ?? _transactionPool.GetByHash(txHash);
                    if(_requiredTxs[txHash] is null)
                    {
                        missingTransactions.Add(txHash);
                    }
                }
            }
            return missingTransactions;
        }

        private void StartFastSync()
        {
            if (_fastSync.IsRunning())
            {
                _fastSync.StartSync(null, null, null);
                return;
            }
            while (true)
            {
                if (_peerHeights.Count == 0)
                {
                    Thread.Sleep(TimeSpan.FromMilliseconds(1_000));
                    continue;
                }
                break;
            }
            var maxHeight = _peerHeights.Values.Max();
            if (IsFastSyncNeeded(_blockManager.GetHeight(), maxHeight))
            {
                // checkpoint should exist
                CheckIfCheckpointExist();
                lock (_peerHashCheckpoint)
                {
                    if (_checkpointExist == false || _checkpointExist is null)
                    {
                        Logger.LogWarning($"Peer has max height: {maxHeight} which suggests peer has checkpoint."
                            + " But cannot get checkpoint. Is peer malicious?");
                        throw new Exception("invalid reply");
                    }
                }
                GetCheckpoint();
                lock (_peerHashCheckpoint)
                {
                    _fastSync.StartSync(_checkpointBlockHeight, _checkpointBlockHash, _stateHashes);
                    _checkpointExist = null;
                    _checkpointBlockHash = null;
                    _checkpointBlockHeight = null;
                    _stateHashes = null;
                }
            }
        }

        private bool IsFastSyncNeeded(ulong myHeight, ulong maxHeight)
        {
            if (myHeight >= maxHeight) return false;
            var syncHeight = CheckpointManager.GetClosestCheckpointHeight(maxHeight);
            return syncHeight > myHeight && syncHeight - myHeight >= CheckpointManager._checkpointPeriod;
        }

        private void GetCheckpoint()
        {
            const int maxPeersToAsk = 1;
            var rnd = new Random();
            while(true)
            {
                var maxHeight = _peerHeights.Values.Count == 0 ? 0 : _peerHeights.Values.Max();
                var peers = _peerHeights
                    .Where(entry => entry.Value >= maxHeight)
                    .Select(entry => entry.Key)
                    .OrderBy(_ => rnd.Next())
                    .Take(maxPeersToAsk)
                    .ToArray();

                Logger.LogTrace($"Sending query for checkpoint to {peers.Length} peers");
                var checkpointTypes = CheckpointUtils.GetAllCheckpointTypes();
                var request = new List<byte>();
                foreach (var checkpointType in checkpointTypes)
                {
                    if (checkpointType != CheckpointType.CheckpointExist)
                    {
                        request.Add((byte) checkpointType);
                    }
                }
                GenerateRequestId();
                var message = _networkManager.MessageFactory.CheckpointRequest(request.ToArray(), _checkpointRequesId!.Value);
                foreach (var peer in peers) _networkManager.SendTo(peer, message);
                lock (_peerHashCheckpoint)
                {
                    var gotReply = Monitor.Wait(_peerHashCheckpoint, TimeSpan.FromMilliseconds(5000));
                    if (gotReply)
                    {
                        return;
                    }
                    _checkpointBlockHeight = null;
                    _checkpointBlockHash = null;
                    _stateHashes = null;
                    ResetRequestId();
                }
            }
        }

        private void CheckIfCheckpointExist()
        {
            const int maxPeersToAsk = 1;
            var rnd = new Random();
            while(true)
            {
                var maxHeight = _peerHeights.Values.Count == 0 ? 0 : _peerHeights.Values.Max();
                var peers = _peerHeights
                    .Where(entry => entry.Value >= maxHeight)
                    .Select(entry => entry.Key)
                    .OrderBy(_ => rnd.Next())
                    .Take(maxPeersToAsk)
                    .ToArray();

                Logger.LogTrace($"Sending query for checkpoint to {peers.Length} peers");
                var request = new byte[1];
                request[0] = (byte) CheckpointType.CheckpointExist;
                GenerateRequestId();
                var message = _networkManager.MessageFactory.CheckpointRequest(request, _checkpointRequesId!.Value);
                foreach (var peer in peers) _networkManager.SendTo(peer, message);
                lock (_peerHashCheckpoint)
                {
                    var gotReply = Monitor.Wait(_peerHashCheckpoint, TimeSpan.FromMilliseconds(5000));
                    if (gotReply)
                    {
                        return;
                    }
                    _checkpointExist = null;
                    ResetRequestId();
                }
            }
        }

        public void HandleCheckpointFromPeer(List<CheckpointInfo> checkpoints, ECDSAPublicKey publicKey, ulong requestId)
        {
            lock (_peerHashCheckpoint)
            {
                // check if the reply matches the request id from request
                if (requestId != _checkpointRequesId) return;
                // remove info from previous reply
                _checkpointExist = null;
                _checkpointBlockHash = null;
                _checkpointBlockHeight = null;
                _stateHashes = null;
                foreach (var checkpointInfo in checkpoints)
                {
                    switch (checkpointInfo.MessageCase)
                    {
                        case CheckpointInfo.MessageOneofCase.CheckpointExist:
                            _checkpointExist = checkpointInfo.CheckpointExist.Exist;
                            break;

                        case CheckpointInfo.MessageOneofCase.CheckpointBlockHeight:
                            _checkpointBlockHeight = checkpointInfo.CheckpointBlockHeight.BlockHeight;
                            break;

                        case CheckpointInfo.MessageOneofCase.CheckpointBlockHash:
                            _checkpointBlockHash = checkpointInfo.CheckpointBlockHash.BlockHash;
                            break;
                        
                        case CheckpointInfo.MessageOneofCase.CheckpointStateHash:
                            if (_stateHashes is null) _stateHashes = new List<(UInt256, CheckpointType)>();
                            var checkpointType = checkpointInfo.CheckpointStateHash.CheckpointType.ToByteArray();
                            _stateHashes.Add(
                                (checkpointInfo.CheckpointStateHash.StateHash,
                                (CheckpointType) checkpointType[0]));
                            break;
                    }
                }
                if (_checkpointBlockHeight != null && !_fastSync.IsCheckpointOk(_checkpointBlockHeight, _checkpointBlockHash, _stateHashes))
                {
                    throw new Exception($"Got invalid checkpoint information from peer: {publicKey.ToHex()}. Is peer malicious?");
                }
                Monitor.PulseAll(_peerHashCheckpoint);
            }
        }

        private void GenerateRequestId()
        {
            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            var random = new byte[8];
            rng.GetBytes(random);
            _checkpointRequesId = SerializationUtils.ToUInt64(random);
        }

        private void ResetRequestId()
        {
            _checkpointRequesId = null;
        }

        public void Dispose()
        {
            _running = false;
            if (_blockSyncThread.ThreadState == ThreadState.Running)
                _blockSyncThread.Join();
            if (_pingThread.ThreadState == ThreadState.Running)
                _pingThread.Join();
        }
    }
}