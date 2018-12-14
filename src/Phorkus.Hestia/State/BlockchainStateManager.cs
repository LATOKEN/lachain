using System;
using Phorkus.Core.Blockchain.State;

namespace Phorkus.Hestia.State
{
    class BlockchainStateManager : IBlockchainStateManager
    {
        public IBlockchainSnapshot LastApprovedSnapshot { get; private set; }
        public IBlockchainSnapshot PendingSnapshot { get; private set; }

        private readonly ISnapshotManager<IBalanceSnapshot> _balanceManager;
        private readonly ISnapshotManager<IAssetSnapshot> _assetManager;

        public BlockchainStateManager(
            ISnapshotManager<IBalanceSnapshot> balanceManager,
            ISnapshotManager<IAssetSnapshot> assetManager
        )
        {
            _balanceManager = balanceManager;
            _assetManager = assetManager;
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

        public void CommitApproved()
        {
            _balanceManager.CommitApproved();
            _assetManager.CommitApproved();
        }
    }
}