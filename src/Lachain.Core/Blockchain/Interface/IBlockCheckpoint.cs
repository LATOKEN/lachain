using Lachain.Proto;
using Lachain.Storage.State;

namespace Lachain.Core.Blockchain.Operations
{
    public interface IBlockCheckpoint
    {
        /// <returns>
        /// Block index of the last checkpoint, null if no checkpoint was saved
        /// </returns>
        ulong? CheckpointBlockId { get; }
        /// <returns>
        /// Block hash of the last checkpoint, null if no checkpoint was saved
        /// </returns>
        UInt256? CheckpointBlockHash { get; }
        /// <summary>
        /// Fetches the last checkpoint-block, null if no checkpoint was saved
        /// </summary>
        Block? GetCheckPointBlock();
        /// <summary>
        /// Takes the snapshot repository type as input and returns the state hash of that snapshot of the last checkpoint
        /// </summary>
        /// <param name = "snapshotType"> Repository type of one of these snapshots: balance, contract, event, storage, transaction, validator </param>
        /// <returns>
        /// State hash of last checkpoint
        /// </returns>
        UInt256? GetStateHashForSnapshot(RepositoryType snapshotType);
        /// <returns>
        /// The period to updated the checkpoint
        /// </returns>
        ulong CheckpointPeriod();
        /// <summary>
        /// Takes the given block as a checkpoint-block and writes necessary information in DB
        /// </summary>
        void SaveCheckpoint(Block block);
        /// <summary>
        /// Checks all checkpoint information: block index, hash and state hash for all six snapshots
        /// </summary>
        /// <returns>
        /// True if consistent, False otherwise
        /// </returns>
        bool IsCheckpointConsistent();
    }
}