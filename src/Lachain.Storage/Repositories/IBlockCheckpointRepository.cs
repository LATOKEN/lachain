using Lachain.Proto;

namespace Lachain.Storage.Repositories
{
    public interface IBlockCheckpointRepository
    {
        /// <returns>
        /// Block Id of last checkpoint
        /// </returns>
        ulong FetchCheckpointBlockId();
        /// <returns>
        /// Saves Checkpoint
        /// </returns>
        void SaveCheckpoint(Block block);
    }
}