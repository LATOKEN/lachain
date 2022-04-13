using Lachain.Proto;

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
            // TODO: save block id, block hash, all six state hash
        }
    }
}