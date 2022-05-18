/*
    We don't add to db everytime we get a node, we accumulate data and make write in batches. FastSyncRepository helps to hold data 
    temporary in memory and periodically flushes it to database. It also handles all db read and write operations.
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Lachain.Core.Blockchain.Checkpoints;
using Lachain.Crypto;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Storage;
using Lachain.Storage.Repositories;
using Lachain.Storage.State;
using Lachain.Storage.Trie;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;


namespace Lachain.Core.Network.FastSync
{
    public class FastSyncRepository : IFastSyncRepository
    {
        private static readonly ILogger<FastSyncRepository>
            Logger = LoggerFactory.GetLoggerForClass<FastSyncRepository>();
        private IDictionary<UInt256, ulong> _idCache = new ConcurrentDictionary<UInt256, ulong>();
        private uint _idCacheCapacity = 100000;
        private const uint _blockAddPeriod = 10000;
        private IDictionary<ulong, IHashTrieNode> _nodeCache = new ConcurrentDictionary<ulong, IHashTrieNode>();
        private uint _nodeCacheCapacity = 5000;
        private readonly IRocksDbContext _dbContext;
        private readonly IStateManager _stateManager;
        private readonly ISnapshotIndexRepository _snapshotIndexRepository;
        private readonly VersionFactory _versionFactory;
        private IBlockchainSnapshot? _blockchainSnapshot;
        private readonly UInt256 EmptyHash = UInt256Utils.Zero;
        public FastSyncRepository(
            IRocksDbContext dbContext,
            IStateManager stateManager,
            IStorageManager storageManager,
            ISnapshotIndexRepository snapshotIndexRepository
        )
        {
            _dbContext = dbContext;
            _stateManager = stateManager;
            _snapshotIndexRepository = snapshotIndexRepository;
            _versionFactory = storageManager.GetVersionFactory();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Initialize(ulong blockNumber, UInt256 blockHash, List<(UInt256, CheckpointType)> stateHashes)
        {
            RocksDbAtomicWrite tx = new RocksDbAtomicWrite(_dbContext);
            tx.Put(EntryPrefix.BlockNumberFromCheckpoint.BuildPrefix(), blockNumber.ToBytes().ToArray());
            tx.Put(EntryPrefix.BlockHashFromCheckpoint.BuildPrefix(), blockHash.ToBytes());
            foreach (var (stateHash, checkpointType) in stateHashes)
            {
                tx.Put(EntryPrefix.StateHashByCheckpointType.BuildPrefix((byte) checkpointType),
                    stateHash.ToBytes());
            }
            ulong zero = 0;
            tx.Put(EntryPrefix.SavedBatch.BuildPrefix(), zero.ToBytes().ToArray());
            tx.Put(EntryPrefix.TotalIncomingBatch.BuildPrefix(), zero.ToBytes().ToArray());
            tx.Put(EntryPrefix.LastDownloadedTries.BuildPrefix(), zero.ToBytes().ToArray());
            tx.Commit();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public ulong GetSavedBatch()
        {
            var rawInfo = _dbContext.Get(EntryPrefix.SavedBatch.BuildPrefix());
            if (rawInfo is null) return 0;
            return SerializationUtils.ToUInt64(rawInfo);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public ulong GetTotalIncomingBatch()
        {
            var rawInfo = _dbContext.Get(EntryPrefix.TotalIncomingBatch.BuildPrefix());
            if (rawInfo is null) return 0;
            return SerializationUtils.ToUInt64(rawInfo);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void UpdateSavedBatch(ulong savedBatch)
        {
            RocksDbAtomicWrite tx = new RocksDbAtomicWrite(_dbContext);
            tx.Delete(EntryPrefix.QueueBatch.BuildPrefix(savedBatch));
            tx.Put(EntryPrefix.SavedBatch.BuildPrefix(), savedBatch.ToBytes().ToArray());
            tx.Commit();
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void SaveIncomingQueueBatch(List<byte> incomingQueue, ulong totalIncomingBatch)
        {
            RocksDbAtomicWrite tx = new RocksDbAtomicWrite(_dbContext);
            tx.Put(EntryPrefix.QueueBatch.BuildPrefix(totalIncomingBatch), incomingQueue.ToArray());
            tx.Put(EntryPrefix.TotalIncomingBatch.BuildPrefix(), totalIncomingBatch.ToBytes().ToArray());
            tx.Commit();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public byte[] GetHashBatchRaw(ulong batch)
        {
            return _dbContext.Get(EntryPrefix.QueueBatch.BuildPrefix(batch));
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryAddNode(TrieNodeInfo nodeInfo)
        {
            UInt256? nodeHash;
            switch (nodeInfo.MessageCase)
            {
                case TrieNodeInfo.MessageOneofCase.InternalNodeInfo:
                    nodeHash = nodeInfo.InternalNodeInfo.Hash;
                    break;
                
                case TrieNodeInfo.MessageOneofCase.LeafNodeInfo:
                    nodeHash =  nodeInfo.LeafNodeInfo.Hash;
                    break;

                default:
                    nodeHash = null;
                    break;
            }
            if (nodeHash is null) return false;
            byte[] nodeHashBytes = nodeHash.ToBytes();
            bool foundId = GetIdByHash(nodeHash, out ulong id);
            IHashTrieNode? trieNode;
            if(foundId)
            {
                bool foundNode = TryGetNode(id, out trieNode);
                if(!foundNode) trieNode = BuildHashTrieNode(nodeInfo);
            }
            else trieNode = BuildHashTrieNode(nodeInfo);
            _nodeCache[id] = trieNode!;
            if(_nodeCache.Count >= _nodeCacheCapacity) Commit();
            return true;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool ExistNode(UInt256 nodeHash)
        {
            if(GetIdByHash(nodeHash, out ulong id))
            {
                return TryGetNode(id, out IHashTrieNode? node);
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool GetIdByHash(UInt256 nodeHash, out ulong id)
        {
            id = 0;
            if(nodeHash.Equals(EmptyHash)) return true;
            if(_idCache.TryGetValue(nodeHash, out id)) return true;
            var idByte = _dbContext.Get(EntryPrefix.VersionByHash.BuildPrefix(nodeHash.ToBytes()));
            if(!(idByte is null)){
                id = UInt64Utils.FromBytes(idByte);
                return true;
            }
            id = _versionFactory.NewVersion();
            _idCache[nodeHash] = id;
            if(_idCache.Count >= _idCacheCapacity) CommitIds();
            return false;
        }


        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryGetNode(ulong id, out IHashTrieNode? trieNode)
        {
            if(_nodeCache.TryGetValue(id, out trieNode)) return true;
            var rawNode = _dbContext.Get(EntryPrefix.PersistentHashMap.BuildPrefix(id));
            trieNode = null;
            if (rawNode is null) return false; 
            trieNode = NodeSerializer.FromBytes(rawNode);
            return true; 
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool IsConsistent(TrieNodeInfo? node, out UInt256? nodeHash)
        {
            nodeHash = null;
            if(node is null) return false;
            switch (node.MessageCase)
            {
                case TrieNodeInfo.MessageOneofCase.InternalNodeInfo:
                    uint mask = node.InternalNodeInfo.ChildrenMask;
                    var hashes = node.InternalNodeInfo.ChildrenHash.ToList();
                    var childrenHashes = new List<byte[]>();
                    foreach (var childHash in hashes)
                    {
                        childrenHashes.Add(childHash.ToBytes());
                    }
                    byte[] hash = childrenHashes
                    .Zip(InternalNode.GetChildrenLabels(mask), (bytes, i) => new[] {i}.Concat(bytes))
                    .SelectMany(bytes => bytes)
                    .KeccakBytes();
                    nodeHash = hash.ToUInt256();
                    return nodeHash.Equals(node.InternalNodeInfo.Hash);

                case TrieNodeInfo.MessageOneofCase.LeafNodeInfo:
                    byte[] keyHash = node.LeafNodeInfo.KeyHash.ToBytes();
                    byte[] value = node.LeafNodeInfo.Value.ToByteArray();
                    byte[] leafHash = keyHash.Length.ToBytes().Concat(keyHash).Concat(value).KeccakBytes(); 
                    nodeHash = leafHash.ToUInt256();
                    return nodeHash.Equals(node.LeafNodeInfo.Hash);

                default:
                    return false;
            }
        }

        private IHashTrieNode BuildHashTrieNode(TrieNodeInfo nodeInfo)
        {
            IHashTrieNode? trieNode;
            switch (nodeInfo.MessageCase)
            {
                case TrieNodeInfo.MessageOneofCase.InternalNodeInfo:
                    var childrenHashes = nodeInfo.InternalNodeInfo.ChildrenHash.ToList();
                    var childrenHashesBytes = new List<byte[]>();
                    var children = new List<ulong>();
                    foreach (var childHash in childrenHashes)
                    {
                        childrenHashesBytes.Add(childHash.ToBytes());
                        bool foundId = GetIdByHash(childHash, out var childId);
                        children.Add(childId);
                    }
                    var mask = nodeInfo.InternalNodeInfo.ChildrenMask;
                    trieNode = new InternalNode(mask, children, childrenHashesBytes);
                    break;

                case TrieNodeInfo.MessageOneofCase.LeafNodeInfo:
                    byte[] keyHash = nodeInfo.LeafNodeInfo.KeyHash.ToBytes();
                    byte[] value = nodeInfo.LeafNodeInfo.Value.ToByteArray();
                    trieNode = new LeafNode(keyHash, value);
                    break;

                default:
                    throw new NullReferenceException("trie-node cannot be null here");
            }
            return trieNode;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public ulong GetCheckpointBlockNumber()
        {
            var rawBlockNumber = _dbContext.Get(EntryPrefix.BlockNumberFromCheckpoint.BuildPrefix());
            if(rawBlockNumber is null) return 0;
            return SerializationUtils.ToUInt64(rawBlockNumber);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public UInt256? GetCheckpointBlockHash()
        {
            var rawBlockHash = _dbContext.Get(EntryPrefix.BlockHashFromCheckpoint.BuildPrefix());
            if (rawBlockHash is null) return null;
            return UInt256Utils.ToUInt256(rawBlockHash);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public UInt256? GetCheckpointStateHash(CheckpointType checkpointType)
        {
            var rawStateHash = _dbContext.Get(EntryPrefix.StateHashByCheckpointType.BuildPrefix((byte) checkpointType));
            if (rawStateHash is null) return null;
            return UInt256Utils.ToUInt256(rawStateHash);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public ulong GetCurrentBlockHeight()
        {
            Initialize();
            return _blockchainSnapshot!.Blocks.GetTotalBlockHeight();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void AddBlock(Block block)
        {
            Initialize();
            _blockchainSnapshot!.Blocks.AddBlock(block);
            if(block.Header.Index%200 == 0) Logger.LogInformation("Added BlockHeader: " + block.Header.Index);
            if(block.Header.Index%_blockAddPeriod == 0)
                _blockchainSnapshot.Blocks.Commit();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public Block? BlockByHeight(ulong height)
        {
            Initialize();
            return _blockchainSnapshot!.Blocks.GetBlockByHeight(height);
        }

        private void CommitNodes()
        {
            RocksDbAtomicWrite tx = new RocksDbAtomicWrite(_dbContext);
            foreach(var item in _nodeCache)
            {
                tx.Put(EntryPrefix.PersistentHashMap.BuildPrefix(item.Key), NodeSerializer.ToBytes(item.Value));
            }
            tx.Commit();
            _nodeCache.Clear();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void CommitIds()
        {
            RocksDbAtomicWrite tx = new RocksDbAtomicWrite(_dbContext);
            foreach(var item in _idCache)
            {
                tx.Put(EntryPrefix.VersionByHash.BuildPrefix(item.Key.ToBytes()), UInt64Utils.ToBytes(item.Value));
            }
            ulong nextVersion = _versionFactory.CurrentVersion + 1;
            tx.Put(EntryPrefix.StorageVersionIndex.BuildPrefix((uint) RepositoryType.MetaRepository), nextVersion.ToBytes().ToArray());
            tx.Commit();
            _idCache.Clear();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Commit()
        {
            CommitIds();
            CommitNodes();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public int GetLastDownloadedTries()
        {
            var rawInfo = _dbContext.Get(EntryPrefix.LastDownloadedTries.BuildPrefix());
            if (rawInfo is null) return 0;
            return SerializationUtils.ToInt32(rawInfo);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void SetLastDownloadedTries(int downloaded)
        {
            _dbContext.Save(EntryPrefix.LastDownloadedTries.BuildPrefix(), downloaded.ToBytes().ToArray());
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public VersionFactory GetVersionFactory()
        {
            return _versionFactory;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void SetSnapshotVersion(string trieName, UInt256 rootHash)
        {
            Initialize();
            bool foundHash = GetIdByHash(rootHash, out ulong trieRoot);
            var snapshot = _blockchainSnapshot!.GetSnapshot(trieName);
            snapshot!.SetCurrentVersion(trieRoot);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void SetState()
        {
            Initialize();
            _stateManager.Approve();
            _stateManager.Commit();
            _snapshotIndexRepository.SaveSnapshotForBlock(
                _blockchainSnapshot!.Blocks.GetTotalBlockHeight(), _blockchainSnapshot);
        }

        private void Initialize()
        {
            if (_blockchainSnapshot is null) _blockchainSnapshot = _stateManager.NewSnapshot();
        }

    }
}
