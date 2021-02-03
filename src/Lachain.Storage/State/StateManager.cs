using System;
using System.Threading;

namespace Lachain.Storage.State
{
    public class StateManager : IStateManager
    {
        // public IBlockchainSnapshot CurrentSnapshot => PendingSnapshot ?? LastApprovedSnapshot;
        public IBlockchainSnapshot CurrentSnapshot
        {
            get
            {
                return PendingSnapshot ?? LastApprovedSnapshot;
            }
            set
            {
                this.CurrentSnapshot = value;
            }
        }

        public IBlockchainSnapshot LastApprovedSnapshot { get; set; }
        public IBlockchainSnapshot? PendingSnapshot { get; private set; }

        private readonly ISnapshotManager<IBlockSnapshot> _blockManager;
        private readonly ISnapshotManager<ITransactionSnapshot> _transactionManager;
        private readonly ISnapshotManager<IBalanceSnapshot> _balanceManager;
        private readonly ISnapshotManager<IContractSnapshot> _contractManager;
        private readonly ISnapshotManager<IStorageSnapshot> _storageManager;
        private readonly ISnapshotManager<IEventSnapshot> _eventManager;
        private readonly ISnapshotManager<IValidatorSnapshot> _validatorManager;

        private readonly Mutex _globalMutex = new Mutex(false);

        public StateManager(IStorageManager storageManager)
        {
            _balanceManager =
                new SnapshotManager<IBalanceSnapshot, BalanceSnapshot>(storageManager,
                    (uint) RepositoryType.BalanceRepository);
            _contractManager =
                new SnapshotManager<IContractSnapshot, ContractSnapshot>(storageManager,
                    (uint) RepositoryType.ContractRepository);
            _storageManager =
                new SnapshotManager<IStorageSnapshot, StorageSnapshot>(storageManager,
                    (uint) RepositoryType.StorageRepository);
            _transactionManager =
                new SnapshotManager<ITransactionSnapshot, TransactionSnapshot>(storageManager,
                    (uint) RepositoryType.TransactionRepository);
            _blockManager =
                new SnapshotManager<IBlockSnapshot, BlockSnapshot>(storageManager,
                    (uint) RepositoryType.BlockRepository);
            _eventManager =
                new SnapshotManager<IEventSnapshot, EventSnapshot>(storageManager,
                    (uint) RepositoryType.EventRepository);
            _validatorManager =
                new SnapshotManager<IValidatorSnapshot, ValidatorSnapshot>(storageManager,
                    (uint) RepositoryType.ValidatorRepository);

            LastApprovedSnapshot = new BlockchainSnapshot(
                _balanceManager.LastApprovedSnapshot,
                _contractManager.LastApprovedSnapshot,
                _storageManager.LastApprovedSnapshot,
                _transactionManager.LastApprovedSnapshot,
                _blockManager.LastApprovedSnapshot,
                _eventManager.LastApprovedSnapshot,
                _validatorManager.LastApprovedSnapshot
            );
        }

        public void SafeContext(Action callback)
        {
            try
            {
                Acquire();
                callback.Invoke();
            }
            finally
            {
                Release();
            }
        }

        public T SafeContext<T>(Func<T> callback)
        {
            try
            {
                Acquire();
                return callback.Invoke();
            }
            finally
            {
                Release();
            }
        }

        public void Acquire()
        {
            _globalMutex.WaitOne();
        }

        public void Release()
        {
            _globalMutex.ReleaseMutex();
        }

        public IBlockchainSnapshot NewSnapshot()
        {
            if (PendingSnapshot != null)
                throw new InvalidOperationException("Cannot begin new snapshot, need to approve or rollback first");
            PendingSnapshot = new BlockchainSnapshot(
                _balanceManager.NewSnapshot(),
                _contractManager.NewSnapshot(),
                _storageManager.NewSnapshot(),
                _transactionManager.NewSnapshot(),
                _blockManager.NewSnapshot(),
                _eventManager.NewSnapshot(),
                _validatorManager.NewSnapshot()
            );
            return PendingSnapshot;
        }

        public void Approve()
        {
            _balanceManager.Approve();
            _contractManager.Approve();
            _storageManager.Approve();
            _transactionManager.Approve();
            _blockManager.Approve();
            _eventManager.Approve();
            _validatorManager.Approve();
            LastApprovedSnapshot = PendingSnapshot ?? throw new InvalidOperationException("Nothing to approve");
            PendingSnapshot = null;
        }

        public void Rollback()
        {
            if (PendingSnapshot == null)
                throw new InvalidOperationException("Nothing to rollback");
            _balanceManager.Rollback();
            _contractManager.Rollback();
            _storageManager.Rollback();
            _transactionManager.Rollback();
            _blockManager.Rollback();
            _eventManager.Rollback();
            _validatorManager.Rollback();
            PendingSnapshot = null;
        }

        public void Commit()
        {
            _balanceManager.Commit();
            _contractManager.Commit();
            _storageManager.Commit();
            _transactionManager.Commit();
            _blockManager.Commit();
            _eventManager.Commit();
            _validatorManager.Commit();
        }

        public void RollbackTo(IBlockchainSnapshot snapshot)
        {
            if (PendingSnapshot != null)
                throw new InvalidOperationException("Cannot rollback to state with unapproved changes");
            _balanceManager.RollbackTo(snapshot.Balances);
            _contractManager.RollbackTo(snapshot.Contracts);
            _storageManager.RollbackTo(snapshot.Storage);
            _transactionManager.RollbackTo(snapshot.Transactions);
            _blockManager.RollbackTo(snapshot.Blocks);
            _eventManager.RollbackTo(snapshot.Events);
            _validatorManager.RollbackTo(snapshot.Validators);
            LastApprovedSnapshot = snapshot;
        }
    }
}