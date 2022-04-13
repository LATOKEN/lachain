using Lachain.Proto;

namespace Lachain.Core.Blockchain.Operations
{
    public interface IBlockCheckpoint
    {
        /// <returns>
        /// Block index of the last checkpoint
        /// </returns>
        ulong CheckpointBlockId { get; }
        /// <returns>
        /// The period to updated the checkpoint
        /// </returns>
        ulong CheckpointPeriod();
        /// <summary>
        /// Takes the given block as a checkpoint-block and writes necessary information in DB
        /// </summary>
        void SaveCheckpointBlock(Block block);
        /// <summary>
        /// Fetches the last checkpoint-block
        /// </summary>
        Block? FetchCheckPointBlock();
    }
}