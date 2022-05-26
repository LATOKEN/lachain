using System.Collections.Generic;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Storage.Repositories;

namespace Lachain.Core.Blockchain.Checkpoint
{
    public interface ICheckpointManager
    {
        /// <returns>
        /// Block index of the last checkpoint, null if no checkpoint was saved
        /// </returns>
        ulong? CheckpointBlockHeight { get; }
        /// <returns>
        /// Block hash of the last checkpoint, null if no checkpoint was saved
        /// </returns>
        UInt256? CheckpointBlockHash { get; }
        /// <summary>
        /// Fetches the last checkpoint-block, null if no checkpoint was saved
        /// </summary>
        UInt256? GetCheckPointBlockHash(ulong blockHeight);
        /// <summary>
        /// Takes the snapshot repository type as input and returns the state hash of that snapshot of the last checkpoint
        /// </summary>
        /// <param name = "snapshotType"> Repository type of one of these snapshots: balance, contract, event, storage, transaction, validator </param>
        /// <param name = "blockHeight"> Height of a checkpoint block </param>
        /// <returns>
        /// State hash of last checkpoint
        /// </returns>
        UInt256? GetStateHashForSnapshotType(RepositoryType snapshotType, ulong blockHeight);
        /// <summary>
        /// Takes the checkpoint type of some snapshot as input and returns the state hash of that snapshot of the last checkpoint
        /// </summary>
        /// <param name = "checkpointType"> Checkpoint type for one of these snapshots: balance, contract, event, storage, transaction, validator </param>
        /// <param name = "blockHeight"> Height of a checkpoint block </param>
        /// <returns>
        /// State hash of last checkpoint
        /// </returns>
        UInt256? GetStateHashForCheckpointType(CheckpointType checkpointType, ulong blockHeight);
        /// <summary>
        /// Takes the checkpoint type of some snapshot as input and returns the state hash of that snapshot of the last checkpoint
        /// </summary>
        /// <param name = "snapshotName"> Trie name of one of these snapshots: balance, contract, event, storage, transaction, validator </param>
        /// <param name = "blockHeight"> Height of a checkpoint block </param>
        /// <returns>
        /// State hash of last checkpoint
        /// </returns>
        UInt256? GetStateHashForSnapshotName(string snapshotName, ulong blockHeight);
        /// <summary>
        /// Saves the given checkpoints in DB. If already saved, checks if the given info is correct
        /// </summary>
        /// <param name = "checkpoints"> List of Checkpoint related info: block height, block hash, state hash </param>
        void AddCheckpoints(List<CheckpointConfigInfo> checkpoints);
        /// <summary>
        /// Fetches all saved checkpoints from DB
        /// </summary>
        /// <returns>
        /// List of CheckpointConfigInfo
        /// </returns>
        List<CheckpointConfigInfo> GetAllSavedCheckpoint();
        /// <summary>
        /// Checks all checkpoint information: block index, hash and state hash for all six snapshots
        /// </summary>
        /// <returns>
        /// True if consistent, False otherwise
        /// </returns>
        bool IsCheckpointConsistent();
        /// <summary>
        /// Given some CheckpointType, gets the corresponding CheckpointInfo for latest checkpoint
        /// </summary>
        /// <param name = "checkpointType"> CheckpointType </param>
        /// <returns>
        /// All required checkpoint info
        /// </returns>
        CheckpointInfo GetCheckpointInfo(CheckpointType checkpointType);
    }
}