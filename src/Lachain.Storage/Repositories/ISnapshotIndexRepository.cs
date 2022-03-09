using Lachain.Storage.State;
using Lachain.Storage.DbCompact;

namespace Lachain.Storage.Repositories
{
    public interface ISnapshotIndexRepository
    {
        IBlockchainSnapshot GetSnapshotForBlock(ulong block);
        void SaveSnapshotForBlock(ulong block, IBlockchainSnapshot snapshot);
        void DeleteVersion(uint repository, ulong block, ulong version, IDbShrinkRepository _repo);
    }
}