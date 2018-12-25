using Phorkus.Storage.State;

namespace Phorkus.Storage.Repositories
{
    public class BalanceManager : SnapshotManager<IBalanceSnapshot, BalanceSnapshot>, ISnapshotManager<IBalanceSnapshot>
    {
        public BalanceManager(IPersistentStorageManager persistentStorageManager) : base(persistentStorageManager)
        {
        }

        protected override uint RepositoryId { get; } = (uint) RepositoryType.BalanceRepository;

        protected override BalanceSnapshot SnaphotFromState(IStorageState state)
        {
            return new BalanceSnapshot(state);
        }
    }
}