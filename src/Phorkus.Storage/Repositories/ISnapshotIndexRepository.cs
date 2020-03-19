using Phorkus.Storage.State;

namespace Phorkus.Storage.Repositories
{
    public interface ISnapshotIndexRepository
    {
        IBlockchainSnapshot GetSnapshotForBlock(ulong block);
        void SaveSnapshotForBlock(ulong block, IBlockchainSnapshot snapshot);
    }
}