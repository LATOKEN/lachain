namespace Phorkus.Storage.State
{
    public interface ISnapshotManager<out T>
    {
        T CurrentSnapshot { get; }
        T LastApprovedSnapshot { get; }
        T PendingSnapshot{ get; }
        
        T NewSnapshot();
        
        /// <summary>
        /// Approve snapshot
        /// </summary>
        void Approve();
        
        /// <summary>
        /// Rollback snapshot
        /// </summary>
        void Rollback();
        
        /// <summary>
        /// Commit already approved snapshot
        /// </summary>
        void Commit();
    }
}