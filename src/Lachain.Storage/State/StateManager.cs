﻿using System;
using System.Threading;
using Lachain.Logger;

namespace Lachain.Storage.State
{
    /*
        This is the main class the core part utilizes to make any changes to the state of the blockchain.
        There are 3 layers of snapshot, 
        (1) Committed Snapshot - this snapshot already persists in disk
        (2) Approved Snapshot - approved but may not persist in the disk 
        (3) Pending Snapshot - this is not yet approved 

        To make any changes to the state, it's required to
            (1) create a new snapshot, (2) make changes to the snapshot
            (3) either approve or rollback()
        approving adds all the changes to the lastApprovedSnapshot
        and rollback() discards all the changes and lastApprovedSnapshot is not changed

        Always write to state inside safe context to avoid concurrency issues.
    */
    public class StateManager : IStateManager
    {
        private static readonly ILogger<StateManager> Logger = LoggerFactory.GetLoggerForClass<StateManager>();
        public IBlockchainSnapshot CurrentSnapshot => PendingSnapshot ?? LastApprovedSnapshot;

        public IBlockchainSnapshot LastApprovedSnapshot { get; private set; }
        public IBlockchainSnapshot? PendingSnapshot { get; private set; }

        private readonly ISnapshotManager<IBlockSnapshot> _blockManager;
        private readonly ISnapshotManager<ITransactionSnapshot> _transactionManager;
        private readonly ISnapshotManager<IBalanceSnapshot> _balanceManager;
        private readonly ISnapshotManager<IContractSnapshot> _contractManager;
        private readonly ISnapshotManager<IStorageSnapshot> _storageManager;
        private readonly ISnapshotManager<IEventSnapshot> _eventManager;
        private readonly ISnapshotManager<IValidatorSnapshot> _validatorManager;

        private readonly Mutex _globalMutex = new Mutex(false);

        private readonly IRocksDbContext _dbContext;

        public StateManager(IStorageManager storageManager, IRocksDbContext dbContext)
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
            _dbContext = dbContext;
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
            if (PendingSnapshot != null)
            {
                Logger.LogError("Nonzero pending snapshot on exit from SafeContext");
                Rollback();
            }
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
            var batch = new RocksDbAtomicWrite(_dbContext);
            _balanceManager.Commit(batch);
            _contractManager.Commit(batch);
            _storageManager.Commit(batch);
            _transactionManager.Commit(batch);
            _blockManager.Commit(batch);
            _eventManager.Commit(batch);
            _validatorManager.Commit(batch);
            batch.Commit();
            ClearCache();
        }

        public void ClearCache()
        {
            _balanceManager.ClearCache();
            _contractManager.ClearCache();
            _storageManager.ClearCache();
            _transactionManager.ClearCache();
            _blockManager.ClearCache();
            _eventManager.ClearCache();
            _validatorManager.ClearCache();
        }
        public void Commit(RocksDbAtomicWrite batch)
        {
            throw new Exception("Invalid function used");
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