using System.Collections.Generic;
using System.Threading.Tasks;
using NeoSharp.Core.Models;
using NeoSharp.Types;

namespace NeoSharp.Core.Storage.Blockchain
{
    public interface IBlockRepository
    {
        /// <summary>
        /// Retrieves a hash by height / index
        /// </summary>
        /// <param name="blockHeight">The block height / index to retrieve</param>
        /// <returns>Block hash at specified height / index</returns>
        Task<BlockHeader> GetBlockHeaderByHeight(uint blockHeight);
        
        /// <summary>
        /// Retrieves a block header by hash
        /// </summary>
        /// <param name="blockHash">Block id / hash</param>
        /// <returns>Block header with specified id</returns>
        Task<BlockHeader> GetBlockHeaderByHash(UInt256 blockHash);
        
        /// <summary>
        /// Returns block hashes by heights specified
        /// </summary>
        /// <param name="heights"></param>
        /// <returns></returns>
        Task<IEnumerable<UInt256>> GetBlockHashesByHeights(IEnumerable<uint> heights);
        
        /// <summary>
        /// Adds a block header to the repository storage
        /// </summary>
        /// <param name="blockHeader">Block header</param>
        Task AddBlockHeader(BlockHeader blockHeader);

        Task<BlockHeader> GetNextBlockHeaderByHash(UInt256 blockHash);
        
        Task<IEnumerable<BlockHeader>> GetBlockHeaderByHashes(IReadOnlyCollection<UInt256> hashes);

        Task<IEnumerable<BlockHeader>> GetBlockHeadersFromHeight(uint height, uint count);
    }
}