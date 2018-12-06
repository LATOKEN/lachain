namespace Phorkus.Core.Blockchain.State
{
    public interface IBalanceManager
    {
        IBalanceSnapshot NewSnapshot();
        IBalanceSnapshot LastApprovedSnapshot { get; }
        IBalanceSnapshot PendingSnapshot{ get; }
        void Approve();
        void Rollback();
        void CommitApproved();
    }
}