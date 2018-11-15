using System.Collections.Generic;
using Phorkus.Core.Proto;

namespace Phorkus.Core.Storage
{
    public interface IBlockRepository
    {
        /// <summary>
        /// Retrieves a hash by height / index
        /// </summary>
        /// <param name="blockHeight">The block height / index to retrieve</param>
        /// <returns>Block hash at specified height / index</returns>
        BlockHeader GetBlockHeaderByHeight(uint blockHeight);
        
        /// <summary>
        /// Retrieves a block header by hash
        /// </summary>
        /// <param name="blockHash">Block id / hash</param>
        /// <returns>Block header with specified id</returns>
        BlockHeader GetBlockHeaderByHash(UInt256 blockHash);
        
        /// <summary>
        /// Returns block hashes by heights specified
        /// </summary>
        /// <param name="heights"></param>
        /// <returns></returns>
        IEnumerable<UInt256> GetBlockHashesByHeights(IEnumerable<uint> heights);
        
        /// <summary>
        /// Adds a block header to the repository storage
        /// </summary>
        /// <param name="blockHeader">Block header</param>
        bool AddBlockHeader(BlockHeader blockHeader);

        BlockHeader GetNextBlockHeaderByHash(UInt256 blockHash);
        
        IEnumerable<BlockHeader> GetBlockHeaderByHashes(IEnumerable<UInt256> hashes);
        
        IEnumerable<BlockHeader> GetBlockHeadersByHeightRange(uint height, uint count);
    }
}