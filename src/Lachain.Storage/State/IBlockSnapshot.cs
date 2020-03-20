using System.Collections.Generic;
using Lachain.Proto;

namespace Lachain.Storage.State
{
    public interface IBlockSnapshot : ISnapshot
    {
        /// <summary>
        /// Retrieves the total / current block height
        /// </summary>
        /// <returns>Total / current block height</returns>
        ulong GetTotalBlockHeight();
        
        /// <summary>
        /// Retrieves a hash by height / index
        /// </summary>
        /// <param name="blockHeight">The block height / index to retrieve</param>
        /// <returns>Block hash at specified height / index</returns>
        Block? GetBlockByHeight(ulong blockHeight);
        
        /// <summary>
        /// Retrieves a block by hash
        /// </summary>
        /// <param name="blockHash">Block id / hash</param>
        /// <returns>Block header with specified id</returns>
        Block? GetBlockByHash(UInt256 blockHash);
        
        /// <summary>
        /// Adds a block header to the repository storage
        /// </summary>
        /// <param name="block">Block header</param>
        void AddBlock(Block block);
        
        IEnumerable<Block> GetBlocksByHeightRange(ulong height, ulong count);

        IEnumerable<Block> GetBlocksByHashes(IEnumerable<UInt256> hashes);
    }
}