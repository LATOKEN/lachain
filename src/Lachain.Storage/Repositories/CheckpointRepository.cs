using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;


namespace Lachain.Storage.Repositories
{
    public class CheckpointRepository : ICheckpointRepository
    {
        private static readonly ILogger<CheckpointRepository> Logger = LoggerFactory.GetLoggerForClass<CheckpointRepository>();
        private readonly IRocksDbContext _rocksDbContext;
        private readonly ISnapshotIndexRepository _snapshotIndexer;

        public CheckpointRepository(
            IRocksDbContext rocksDbContext,
            ISnapshotIndexRepository snapshotIndexer
        )
        {
            _rocksDbContext = rocksDbContext;
            _snapshotIndexer = snapshotIndexer;
        }

        public List<ulong> FetchCheckpointBlockHeights()
        {
            var key = EntryPrefix.CheckpointBlockHeights.BuildPrefix();
            var rawInfo = _rocksDbContext.Get(key);
            if (rawInfo is null) return new List<ulong>();
            return SerializationUtils.ToUInt64Array(rawInfo).ToList();
        }

        public UInt256? FetchCheckpointBlockHash(ulong blockHeight)
        {
            var key = EntryPrefix.CheckpointBlockHash.BuildPrefix(blockHeight);
            var blockHash = _rocksDbContext.Get(key);
            if (blockHash is null) return null;
            return blockHash.ToUInt256();
        }
        
        public UInt256? FetchSnapshotStateHash(RepositoryType repositoryId, ulong blockHeight)
        {
            var key = EntryPrefix.CheckpointSnapshotState.BuildPrefix(
                ((uint) repositoryId).ToBytes().Concat(blockHeight.ToBytes()).ToArray());
            var stateHash = _rocksDbContext.Get(key);
            if (stateHash is null) return null;
            return stateHash.ToUInt256();
        }

        public bool SaveCheckpoint(Block block)
        {
            try
            {
                var batch = new RocksDbAtomicWrite(_rocksDbContext);
                var blockchainSnapshot = _snapshotIndexer.GetSnapshotForBlock(block.Header.Index);
                var snapshots = blockchainSnapshot.GetAllSnapshot();
                foreach (var snapshot in snapshots)
                {
                    if (snapshot.RepositoryId == (uint) RepositoryType.BlockRepository) continue;
                    SaveSnapshotStateHash(batch, snapshot, block.Header.Index);
                }
                SaveBlockHeight(batch, block.Header.Index);
                SaveBlockHash(batch, block.Hash, block.Header.Index);
                batch.Commit();
                return true;
            }
            catch (Exception exception)
            {
                Logger.LogWarning($"Could not save checkpoint for block {block.Header.Index}"
                    +$" with hash {block.Hash.ToHex()}. Exception: {exception}");
                return false;
            }
        }

        private void SaveBlockHeight(RocksDbAtomicWrite batch, ulong blockHeight)
        {
            var key = EntryPrefix.CheckpointBlockHeights.BuildPrefix();
            var rawInfo = _rocksDbContext.Get(key);
            var list = new List<ulong>();
            if (!(rawInfo is null)) list = SerializationUtils.ToUInt64Array(rawInfo).ToList();
            list.Add(blockHeight);
            batch.Put(key, SerializationUtils.ToBytes(list.ToArray()));
        }

        private void SaveBlockHash(RocksDbAtomicWrite batch, UInt256 blockHash, ulong blockHeight)
        {
            var key = EntryPrefix.CheckpointBlockHash.BuildPrefix(blockHeight);
            var value = blockHash.ToBytes();
            batch.Put(key, value);
        }

        private void SaveSnapshotStateHash(RocksDbAtomicWrite batch, ISnapshot snapshot, ulong blockHeight)
        {
            var key = EntryPrefix.CheckpointSnapshotState.BuildPrefix(
                snapshot.RepositoryId.ToBytes().Concat(blockHeight.ToBytes()).ToArray());
            var stateHash = snapshot.Hash.ToBytes();
            batch.Put(key, stateHash);
        }
    }
}