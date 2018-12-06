namespace Phorkus.Core.Blockchain.State
{
    public interface IBlockchainStateManager
    {
        IBlockchainSnapshot NewSnapshot();
        IBlockchainSnapshot LastApprovedSnapshot { get; }
        IBlockchainSnapshot PendingSnapshot{ get; }
        void Approve();
        void Rollback();
        void CommitApproved();
    }
}