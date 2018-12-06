using System;
using Phorkus.Core.Blockchain.State;

namespace Phorkus.Hestia.State
{
    class BlockchainStateManager : IBlockchainStateManager
    {
        public IBlockchainSnapshot LastApprovedSnapshot { get; private set; }
        public IBlockchainSnapshot PendingSnapshot { get; private set; }

        private readonly IBalanceManager _balanceManager;

        public BlockchainStateManager(IBalanceManager balanceManager)
        {
            _balanceManager = balanceManager;
            LastApprovedSnapshot = new BlockchainSnapshot(_balanceManager.LastApprovedSnapshot);
        }

        public IBlockchainSnapshot NewSnapshot()
        {
            if (PendingSnapshot != null)
                throw new InvalidOperationException("Cannot begin new snapshot, need to approve or rollback first");
            PendingSnapshot = new BlockchainSnapshot(_balanceManager.NewSnapshot());
            return PendingSnapshot;
        }

        public void Approve()
        {
            _balanceManager.Approve();
            LastApprovedSnapshot = PendingSnapshot ?? throw new InvalidOperationException("Nothing to approve");
            PendingSnapshot = null;
        }

        public void Rollback()
        {
            if (PendingSnapshot != null) throw new InvalidOperationException("Nothing to rollback");
            _balanceManager.Rollback();
            PendingSnapshot = null;
        }

        public void CommitApproved()
        {
            _balanceManager.CommitApproved();
        }
    }
}