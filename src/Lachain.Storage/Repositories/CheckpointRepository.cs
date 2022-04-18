using System;
using Lachain.Proto;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using Lachain.Storage.State;
using Lachain.Logger;

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

        public ulong? FetchCheckpointBlockId()
        {
            var key = EntryPrefix.CheckpointBlockHeight.BuildPrefix();
            var blockId = _rocksDbContext.Get(key);
            if (blockId is null) return null;
            return UInt64Utils.FromBytes(blockId);
        }

        public UInt256? FetchCheckpointBlockHash()
        {
            var key = EntryPrefix.CheckpointBlockHash.BuildPrefix();
            var blockHash = _rocksDbContext.Get(key);
            if (blockHash is null) return null;
            return blockHash.ToUInt256();
        }
        
        public UInt256? FetchSnapshotStateHash(RepositoryType repositoryId)
        {
            var key = EntryPrefix.CheckpointSnapshotState.BuildPrefix((uint) repositoryId);
            var stateHash = _rocksDbContext.Get(key);
            if (stateHash is null) return null;
            return stateHash.ToUInt256();
        }

        public void SaveCheckpoint(Block block)
        {
            try
            {
                var batch = new RocksDbAtomicWrite(_rocksDbContext);
                var blockchainSnapshot = _snapshotIndexer.GetSnapshotForBlock(block.Header.Index);
                var snapshots = blockchainSnapshot.GetAllSnapshot();
                foreach (var snapshot in snapshots)
                {
                    if (snapshot.RepositoryId == (uint) RepositoryType.BlockRepository) continue;
                    SaveSnapshotStateHash(batch, snapshot);
                }
                SaveBlockId(batch, block.Header.Index);
                SaveBlockHash(batch, block.Hash);
                batch.Commit();
            }
            catch (Exception exception)
            {
                Logger.LogWarning($"Could not save checkpoint for block {block.Header.Index}"
                    +$" with hash {block.Hash.ToHex()}. Exception: {exception}");
            }
        }

        private void SaveBlockId(RocksDbAtomicWrite batch, ulong blockId)
        {
            var key = EntryPrefix.CheckpointBlockHeight.BuildPrefix();
            var value = UInt64Utils.ToBytes(blockId);
            batch.Put(key, value);
        }

        private void SaveBlockHash(RocksDbAtomicWrite batch, UInt256 blockHash)
        {
            var key = EntryPrefix.CheckpointBlockHash.BuildPrefix();
            var value = blockHash.ToBytes();
            batch.Put(key, value);
        }

        private void SaveSnapshotStateHash(RocksDbAtomicWrite batch, ISnapshot snapshot)
        {
            var key = EntryPrefix.CheckpointSnapshotState.BuildPrefix(snapshot.RepositoryId);
            var stateHash = snapshot.Hash.ToBytes();
            batch.Put(key, stateHash);
        }
    }
}