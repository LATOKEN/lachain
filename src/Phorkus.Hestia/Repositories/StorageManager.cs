using Phorkus.Core.Blockchain.State;
using Phorkus.Hestia.State;

namespace Phorkus.Hestia.Repositories
{
    public class StorageManager : SnapshotManager<IStorageSnapshot, StorageSnapshot>, ISnapshotManager<IStorageSnapshot>
    {
        public StorageManager(IPersistentStorageManager persistentStorageManager) : base(persistentStorageManager)
        {
        }

        protected override uint RepositoryId { get; } = (uint) RepositoryType.StorageRepository;

        protected override StorageSnapshot SnaphotFromState(IStorageState state)
        {
            return new StorageSnapshot(state);
        }
    }
}