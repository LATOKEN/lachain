using Phorkus.Core.Blockchain.State;
using Phorkus.Hestia.State;

namespace Phorkus.Hestia.Repositories
{
    public class AssetManager : SnapshotManager<IAssetSnapshot, AssetSnapshot>, ISnapshotManager<IAssetSnapshot>
    {
        public AssetManager(IPersistentStorageManager persistentStorageManager) : base(persistentStorageManager)
        {
        }

        protected override uint RepositoryId { get; } = (uint) RepositoryType.AssetRepository;

        protected override AssetSnapshot SnaphotFromState(IStorageState state)
        {
            return new AssetSnapshot(state);
        }
    }
}