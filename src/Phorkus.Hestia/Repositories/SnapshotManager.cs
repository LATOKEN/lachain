using System;

namespace Phorkus.Hestia.Repositories
{
    public abstract class SnapshotManager<TSnapshotInterface, TSnapshotType> 
        where TSnapshotType : class, TSnapshotInterface, ISnapshot
    {
        private readonly IStorageManager _storageManager;
        private TSnapshotType _lastApprovedSnapshot;
        private TSnapshotType _pendingSnapshot;
        protected abstract uint RepositoryId { get; }

        public TSnapshotInterface LastApprovedSnapshot => _lastApprovedSnapshot;
        public TSnapshotInterface PendingSnapshot => _pendingSnapshot;

        protected abstract TSnapshotType SnaphotFromState(IStorageState state);

        protected SnapshotManager(IStorageManager storageManager)
        {
            _storageManager = storageManager;
            _lastApprovedSnapshot = SnaphotFromState(_storageManager.GetLastState(RepositoryId)); 
            _pendingSnapshot = null;
        }

        public TSnapshotInterface NewSnapshot()
        {
            if (PendingSnapshot != null)
                throw new InvalidOperationException("Cannot begin new snapshot, need to approve or rollback first");
            _pendingSnapshot = SnaphotFromState(_storageManager.GetState(RepositoryId, _lastApprovedSnapshot.Version));
            return PendingSnapshot;
        }

        public void Approve()
        {
            _lastApprovedSnapshot = _pendingSnapshot ?? throw new InvalidOperationException("Nothing to approve");
            _pendingSnapshot = null;
        }

        public void Rollback()
        {
            if (PendingSnapshot == null) throw new InvalidOperationException("Nothing to rollback");
            _pendingSnapshot = null;
        }

        public void CommitApproved()
        {
            _lastApprovedSnapshot.Commit();
        }
    }
}