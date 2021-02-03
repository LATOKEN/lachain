﻿namespace Lachain.Storage.State
{
    public interface ISnapshotManager<T> where T: class
    {
        T CurrentSnapshot { get; set; }
        T LastApprovedSnapshot { get; set; }
        T? PendingSnapshot{ get; }
        
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

        /// <summary>
        /// Rollback to specific version (in approved but uncommited state)
        /// </summary>
        void RollbackTo(T snapshot);
    }
}