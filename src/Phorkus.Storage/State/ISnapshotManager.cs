namespace Phorkus.Storage.State
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