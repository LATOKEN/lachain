using System.Linq;
using Lachain.Proto;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;

namespace Lachain.Storage.Repositories
{
    public class BlockCheckpointRepository : IBlockCheckpointRepository
    {
        private readonly IRocksDbContext _rocksDbContext;
        private readonly ISnapshotIndexRepository _snapshotIndexer;
        public BlockCheckpointRepository(
            IRocksDbContext rocksDbContext,
            ISnapshotIndexRepository snapshotIndexer
        )
        {
            _rocksDbContext = rocksDbContext;
            _snapshotIndexer = snapshotIndexer;
        }
        public ulong FetchCheckpointBlockId()
        {
            return 0;
            // TODO: get block id
        }
        public void SaveCheckpoint(Block block)
        {
            RocksDbAtomicWrite batch = new RocksDbAtomicWrite(_rocksDbContext);
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

        private void SaveBalanceState(RocksDbAtomicWrite batch, UInt256 stateHash)
        {
            
        }
    }
}