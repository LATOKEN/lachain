using System.Collections.Generic;
using Phorkus.Proto;

namespace Phorkus.Core.Storage
{
    public interface IBlockRepository
    {
        /// <summary>
        /// Retrieves a hash by height / index
        /// </summary>
        /// <param name="blockHeight">The block height / index to retrieve</param>
        /// <returns>Block hash at specified height / index</returns>
        Block GetBlockByHeight(ulong blockHeight);
        
        /// <summary>
        /// Retrieves a block by hash
        /// </summary>
        /// <param name="blockHash">Block id / hash</param>
        /// <returns>Block header with specified id</returns>
        Block GetBlockByHash(UInt256 blockHash);
        
        /// <summary>
        /// Returns block hashes by heights specified
        /// </summary>
        /// <param name="heights"></param>
        /// <returns></returns>
        IEnumerable<UInt256> GetBlockHashesByHeights(IEnumerable<uint> heights);
        
        /// <summary>
        /// Adds a block header to the repository storage
        /// </summary>
        /// <param name="block">Block header</param>
        bool AddBlock(Block block);

        Block GetNextBlockByHash(UInt256 blockHash);
        
        IEnumerable<Block> GetBlocksByHeightRange(ulong height, ulong count);

        IEnumerable<Block> GetBlocksByHashes(IEnumerable<UInt256> hashes);
    }
}