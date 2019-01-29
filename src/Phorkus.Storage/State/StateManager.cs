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
        private readonly ISnapshotManager<IContractSnapshot> _contractManager;
        private readonly ISnapshotManager<IContractStorageSnapshot> _contractStorageManager;

        public StateManager(IStorageManager storageManager)
        {
            _balanceManager = new SnapshotManager<IBalanceSnapshot, BalanceSnapshot>(storageManager, (uint) RepositoryType.BalanceRepository);
            _assetManager = new SnapshotManager<IAssetSnapshot, AssetSnapshot>(storageManager, (uint) RepositoryType.AssetRepository);
            _contractManager = new SnapshotManager<IContractSnapshot, ContractSnapshot>(storageManager, (uint) RepositoryType.ContractRepository);
            _contractStorageManager = new SnapshotManager<IContractStorageSnapshot, ContractStorageSnapshot>(storageManager, (uint) RepositoryType.ContractStorageRepository);
            
            LastApprovedSnapshot = new BlockchainSnapshot(
                _balanceManager.LastApprovedSnapshot,
                _assetManager.LastApprovedSnapshot,
                _contractManager.LastApprovedSnapshot,
                _contractStorageManager.LastApprovedSnapshot
            );
        }
        
        public IBlockchainSnapshot NewSnapshot()
        {
            if (PendingSnapshot != null)
                throw new InvalidOperationException("Cannot begin new snapshot, need to approve or rollback first");
            PendingSnapshot = new BlockchainSnapshot(
                _balanceManager.NewSnapshot(),
                _assetManager.NewSnapshot(),
                _contractManager.NewSnapshot(),
                _contractStorageManager.NewSnapshot()
            );
            return PendingSnapshot;
        }

        public void Approve()
        {
            _balanceManager.Approve();
            _assetManager.Approve();
            _contractManager.Approve();
            _contractStorageManager.Approve();
            LastApprovedSnapshot = PendingSnapshot ?? throw new InvalidOperationException("Nothing to approve");
            PendingSnapshot = null;
        }

        public void Rollback()
        {
            if (PendingSnapshot == null)
                throw new InvalidOperationException("Nothing to rollback");
            _balanceManager.Rollback();
            _assetManager.Rollback();
            _contractManager.Rollback();
            _contractStorageManager.Rollback();
            PendingSnapshot = null;
        }

        public void Commit()
        {
            _balanceManager.Commit();
            _assetManager.Commit();
            _contractManager.Commit();
            _contractStorageManager.Commit();
        }
    }
}