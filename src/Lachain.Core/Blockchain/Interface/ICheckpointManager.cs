using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Storage.Repositories;
using System.Collections.Generic;

namespace Lachain.Core.Blockchain.Operations
{
    public interface ICheckpointManager
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
        UInt256? GetStateHashForSnapshotType(RepositoryType snapshotType);
        /// <summary>
        /// Takes the checkpoint type of some snapshot as input and returns the state hash of that snapshot of the last checkpoint
        /// </summary>
        /// <param name = "checkpointType"> Checkpoint type for one of these snapshots: balance, contract, event, storage, transaction, validator </param>
        /// <returns>
        /// State hash of last checkpoint
        /// </returns>
        UInt256? GetStateHashForCheckpointType(CheckpointType checkpointType);
        /// <summary>
        /// Takes the checkpoint type of some snapshot as input and returns the state hash of that snapshot of the last checkpoint
        /// </summary>
        /// <param name = "snapshotName"> Trie name of one of these snapshots: balance, contract, event, storage, transaction, validator </param>
        /// <returns>
        /// State hash of last checkpoint
        /// </returns>
        UInt256? GetStateHashForSnapshotName(string snapshotName);
        /// <summary>
        /// Takes the given block as a checkpoint-block and writes necessary information in DB
        /// </summary>
        /// <param name = "block"> Block </param>
        /// <returns>
        /// True if checkpoint is saved successfully, False otherwise
        /// </returns>
        bool SaveCheckpoint(Block block);
        /// <summary>
        /// Checks all checkpoint information: block index, hash and state hash for all six snapshots
        /// </summary>
        /// <returns>
        /// True if consistent, False otherwise
        /// </returns>
        bool IsCheckpointConsistent();
        /// <summary>
        /// Given some CheckpointType, gets the corresponding CheckpointInfo
        /// </summary>
        /// <param name = "checkpointType"> CheckpointType </param>
        /// <returns>
        /// True if consistent, False otherwise
        /// </returns>
        CheckpointInfo GetCheckpointInfo(CheckpointType checkpointType);
    }
}