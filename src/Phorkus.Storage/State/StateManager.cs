using System;

namespace Phorkus.Storage.State
{
    public class StateManager : IStateManager
    {
        public IBlockchainSnapshot CurrentSnapshot => PendingSnapshot ?? LastApprovedSnapshot;

        public IBlockchainSnapshot LastApprovedSnapshot { get; private set; }
        public IBlockchainSnapshot PendingSnapshot { get; private set; }

        private readonly ISnapshotManager<IBalanceSnapshot> _balanceManager;
        private readonly ISnapshotManager<IAssetSnapshot> _assetManager;

        public StateManager(IStorageManager storageManager)
        {
            _balanceManager = new SnapshotManager<IBalanceSnapshot, BalanceSnapshot>(storageManager, (uint) RepositoryType.BalanceRepository);
            _assetManager = new SnapshotManager<IAssetSnapshot, AssetSnapshot>(storageManager, (uint) RepositoryType.AssetRepository);
            
            LastApprovedSnapshot = new BlockchainSnapshot(
                _balanceManager.LastApprovedSnapshot,
                _assetManager.LastApprovedSnapshot
            );
        }
        
        public IBlockchainSnapshot NewSnapshot()
        {
            if (PendingSnapshot != null)
                throw new InvalidOperationException("Cannot begin new snapshot, need to approve or rollback first");
            PendingSnapshot = new BlockchainSnapshot(
                _balanceManager.NewSnapshot(),
                _assetManager.NewSnapshot()
            );
            return PendingSnapshot;
        }

        public void Approve()
        {
            _balanceManager.Approve();
            _assetManager.Approve();
            LastApprovedSnapshot = PendingSnapshot ?? throw new InvalidOperationException("Nothing to approve");
            PendingSnapshot = null;
        }

        public void Rollback()
        {
            if (PendingSnapshot == null)
                throw new InvalidOperationException("Nothing to rollback");
            _balanceManager.Rollback();
            _assetManager.Rollback();
            PendingSnapshot = null;
        }

        public void Commit()
        {
            _balanceManager.Commit();
            _assetManager.Commit();
        }
    }
}