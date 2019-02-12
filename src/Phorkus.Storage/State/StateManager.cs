using System;
using System.Threading;

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
        private readonly ISnapshotManager<IStorageSnapshot> _storageManager;
        private readonly ISnapshotManager<ITransactionSnapshot> _transactionManager;
        private readonly ISnapshotManager<IBlockSnapshot> _blockManager;
        private readonly ISnapshotManager<IWithdrawalSnapshot> _withdrawalManager;

        private readonly Mutex _globalMutex
            = new Mutex(false);
        
        public StateManager(IStorageManager storageManager)
        {
            _balanceManager = new SnapshotManager<IBalanceSnapshot, BalanceSnapshot>(storageManager, (uint) RepositoryType.BalanceRepository);
            _assetManager = new SnapshotManager<IAssetSnapshot, AssetSnapshot>(storageManager, (uint) RepositoryType.AssetRepository);
            _contractManager = new SnapshotManager<IContractSnapshot, ContractSnapshot>(storageManager, (uint) RepositoryType.ContractRepository);
            _storageManager = new SnapshotManager<IStorageSnapshot, StorageSnapshot>(storageManager, (uint) RepositoryType.StorageRepository);
            _transactionManager = new SnapshotManager<ITransactionSnapshot, TransactionSnapshot>(storageManager, (uint) RepositoryType.TransactionRepository);
            _blockManager = new SnapshotManager<IBlockSnapshot, BlockSnapshot>(storageManager, (uint) RepositoryType.BlockRepository);
            _withdrawalManager = new SnapshotManager<IWithdrawalSnapshot, WithdrawalSnapshot>(storageManager, (uint) RepositoryType.WithdrawalRepository);
            
            LastApprovedSnapshot = new BlockchainSnapshot(
                _balanceManager.LastApprovedSnapshot,
                _assetManager.LastApprovedSnapshot,
                _contractManager.LastApprovedSnapshot,
                _storageManager.LastApprovedSnapshot,
                _transactionManager.LastApprovedSnapshot,
                _blockManager.LastApprovedSnapshot,
                _withdrawalManager.LastApprovedSnapshot
            );
        }
        
        public IBlockchainSnapshot NewSnapshot()
        {
            _globalMutex.WaitOne();
            
            if (PendingSnapshot != null)
                throw new InvalidOperationException("Cannot begin new snapshot, need to approve or rollback first");
            PendingSnapshot = new BlockchainSnapshot(
                _balanceManager.NewSnapshot(),
                _assetManager.NewSnapshot(),
                _contractManager.NewSnapshot(),
                _storageManager.NewSnapshot(),
                _transactionManager.NewSnapshot(),
                _blockManager.NewSnapshot(),
                _withdrawalManager.NewSnapshot()
            );
            return PendingSnapshot;
        }

        public void Approve()
        {
            try
            {
                _balanceManager.Approve();
                _assetManager.Approve();
                _contractManager.Approve();
                _storageManager.Approve();
                _transactionManager.Approve();
                _blockManager.Approve();
                _withdrawalManager.Approve();
                LastApprovedSnapshot = PendingSnapshot ?? throw new InvalidOperationException("Nothing to approve");
                PendingSnapshot = null;
            }
            finally
            {
                _globalMutex.ReleaseMutex();
            }
        }
        
        public void Rollback()
        {
            try
            {
                if (PendingSnapshot == null)
                    throw new InvalidOperationException("Nothing to rollback");
                _balanceManager.Rollback();
                _assetManager.Rollback();
                _contractManager.Rollback();
                _storageManager.Rollback();
                _transactionManager.Rollback();
                _blockManager.Rollback();
                _withdrawalManager.Rollback();
                PendingSnapshot = null;
            }
            finally
            {
                _globalMutex.ReleaseMutex();
            }
        }

        public void Commit()
        {
            try
            {
                _balanceManager.Commit();
                _assetManager.Commit();
                _contractManager.Commit();
                _storageManager.Commit();
                _transactionManager.Commit();
                _blockManager.Commit();
                _withdrawalManager.Commit();
            }
            finally
            {
                try
                {
                    _globalMutex.ReleaseMutex();
                }
                catch (ApplicationException)
                {
                    // ignore
                }
            }
        }
    }
}