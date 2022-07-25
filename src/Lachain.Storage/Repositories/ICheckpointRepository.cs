using System.Collections.Generic;
using Lachain.Storage.State;
using Lachain.Proto;

namespace Lachain.Storage.Repositories
{
    public interface ICheckpointRepository
    {
        /// <summary>
        /// Tries to save checkpoint for given block
        /// </summary>
        /// <param name = "block"> Block </param>
        /// <returns>
        /// True if checkpoint is saved successfully, False otherwise
        /// </returns>
        bool SaveCheckpoint(Block block);
        /// <returns>
        /// Block Id of last checkpoint, null if no checkpoint was saved
        /// </returns>
        List<ulong> FetchCheckpointBlockHeights();
        /// <param name = "blockHeight"> Block height for which the block hash is needed </param>
        /// <returns>
        /// Block hash of last checkpoint, null if no checkpoint was saved
        /// </returns>   
        UInt256? FetchCheckpointBlockHash(ulong blockHeight);
        /// <summary>
        /// Takes the snapshot repository type as input and fetches the state hash of that snapshot of the last checkpoint
        /// </summary>
        /// <param name = "repositoryId"> Repository type of one of these snapshots: balance, contract, event, storage, transaction, validator </param>
        /// <param name = "blockHeight"> Block height for which the state hash is needed </param>
        /// <returns>
        /// State hash of last checkpoint
        /// </returns>
        UInt256? FetchSnapshotStateHash(RepositoryType repositoryId, ulong blockHeight);
    }
}