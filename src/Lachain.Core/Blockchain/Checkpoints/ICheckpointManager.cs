using System.Collections.Generic;
using Lachain.Proto;
using Lachain.Storage.State;

namespace Lachain.Core.Blockchain.Checkpoints
{
    public interface ICheckpointManager
    {
        ulong GetCheckpointBlockHeight();
        /// <summary>
        /// Fetches block hash of last checkpoint
        /// </summary>
        /// <returns>
        /// Block hash of checkpoint <c>blockHeight</c>
        /// </returns>
        UInt256? GetCheckpointBlockHash();
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
        /// Checks if the given info is correct and adds them
        /// </summary>
        /// <param name = "checkpoints"> List of Checkpoint related info: block height, block hash, state hash </param>
        void VerifyAndAddCheckpoints(List<CheckpointConfigInfo> checkpoints);
        /// <summary>
        /// Fetches all checkpoints
        /// </summary>
        /// <returns>
        /// List of Checkpoint
        /// </returns>
        List<Checkpoint> GetAllCheckpoints();
        /// if </c>height</c> is a checkpoint block then fetches its info: BlockHash, StateHashes 
        /// otherwise returns null
        /// </summary>
        /// <param name = "checkpointType"> CheckpointType </param>
        /// <param name = "height"> Block Height </param>
        /// <returns>
        /// All required checkpoint info
        /// </returns>
        CheckpointInfo GetCheckpointInfo(CheckpointType checkpointType);
    }
}