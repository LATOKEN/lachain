using System.Threading.Tasks;

namespace NeoSharp.Core.Storage.Blockchain
{
    public interface IGlobalRepository
    {
        /// <summary>
        /// Retrieves the total / current block height
        /// </summary>
        /// <returns>Total / current block height</returns>
        Task<uint> GetTotalBlockHeight();
        
        /// <summary>
        /// Set the total/ current block height
        /// </summary>
        /// <param name="height">Total / current block height</param>
        Task SetTotalBlockHeight(uint height);
        
        /// <summary>
        /// Retrieves the total / current block header height
        /// </summary>
        /// <returns>Total / current block header height</returns>
        Task<uint> GetTotalBlockHeaderHeight();

        /// <summary>
        /// Set the total/ current block header height
        /// </summary>
        /// <param name="height">Total / current block header height</param>
        Task SetTotalBlockHeaderHeight(uint height);

        /// <summary>
        /// Gets the version of the blockchain DB
        /// </summary>
        /// <returns></returns>
        Task<string> GetVersion();
        
        /// <summary>
        /// Sets the version of the blockchain DB
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        Task SetVersion(string version);
    }
}