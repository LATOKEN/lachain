namespace Phorkus.Core.Blockchain.State
{
    public interface ISnapshotManager<out T>
    {
        T NewSnapshot();
        T LastApprovedSnapshot { get; }
        T PendingSnapshot{ get; }
        void Approve();
        void Rollback();
        void CommitApproved();
    }
}