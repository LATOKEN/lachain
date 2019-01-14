using System;

namespace Phorkus.Storage.State
{
    public class SnapshotManager<TSnapshotInterface, TSnapshotType> : ISnapshotManager<TSnapshotInterface>
        where TSnapshotType : class, TSnapshotInterface, ISnapshot where TSnapshotInterface : class
    {
        private readonly IStorageManager _storageManager;
        
        private TSnapshotType _lastApprovedSnapshot;
        private TSnapshotType _pendingSnapshot;
        
        protected uint RepositoryId { get; }

        public TSnapshotInterface CurrentSnapshot => PendingSnapshot ?? LastApprovedSnapshot;
        
        public TSnapshotInterface LastApprovedSnapshot => _lastApprovedSnapshot;
        public TSnapshotInterface PendingSnapshot => _pendingSnapshot;
        
        private static TSnapshotType SnaphotFromState(IStorageState state)
        {
            return (TSnapshotType) Activator.CreateInstance(typeof(TSnapshotType), state);
        }
        
        public SnapshotManager(IStorageManager storageManager, uint repositoryId)
        {
            _storageManager = storageManager;
            _lastApprovedSnapshot = SnaphotFromState(_storageManager.GetLastState(repositoryId)); 
            _pendingSnapshot = null;
            RepositoryId = repositoryId;
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
            if (PendingSnapshot == null)
                throw new InvalidOperationException("Nothing to rollback");
            _pendingSnapshot = null;
        }

        public void Commit()
        {
            _lastApprovedSnapshot.Commit();
        }
    }
}