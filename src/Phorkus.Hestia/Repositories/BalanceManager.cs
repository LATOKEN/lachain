using System;
using Phorkus.Core.Blockchain.State;

namespace Phorkus.Hestia.Repositories
{
    public class BalanceManager : IBalanceManager
    {
        private readonly IStorageManager _storageManager;
        private const uint RepositoryId = (uint) RepositoryType.BalanceRepository;

        public IBalanceSnapshot LastApprovedSnapshot { get; private set; }
        public IBalanceSnapshot PendingSnapshot { get; private set; }

        public BalanceManager(IStorageManager storageManager)
        {
            _storageManager = storageManager;
            LastApprovedSnapshot = new BalanceSnapshot(_storageManager.GetLastState(RepositoryId));
            PendingSnapshot = null;
        }

        public IBalanceSnapshot NewSnapshot()
        {
            if (PendingSnapshot != null)
                throw new InvalidOperationException("Cannot begin new snapshot, need to approve or rollback first");
            PendingSnapshot = new BalanceSnapshot(_storageManager.GetState(RepositoryId, LastApprovedSnapshot.Version));
            return PendingSnapshot;
        }

        public void Approve()
        {
            LastApprovedSnapshot = PendingSnapshot ?? throw new InvalidOperationException("Nothing to approve");
            PendingSnapshot = null;
        }

        public void Rollback()
        {
            if (PendingSnapshot == null) throw new InvalidOperationException("Nothing to rollback");
            PendingSnapshot = null;
        }

        public void CommitApproved()
        {
            LastApprovedSnapshot.Commit();
        }
    }
}