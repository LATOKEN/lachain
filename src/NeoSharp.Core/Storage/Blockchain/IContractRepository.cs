using System.Threading.Tasks;
using NeoSharp.Core.Models;
using NeoSharp.Types;

namespace NeoSharp.Core.Storage.Blockchain
{
    public interface IContractRepository
    {
        /// <summary>
        /// Retrieves a smart contract by its hash
        /// </summary>
        /// <param name="contractHash"></param>
        /// <returns></returns>
        Task<Contract> GetContractByHash(UInt160 contractHash);
        
        /// <summary>
        /// Adds a smart contract
        /// </summary>
        /// <param name="contract"></param>
        /// <returns></returns>
        Task AddContract(Contract contract);

        /// <summary>
        /// Delete a smart contract by its hash
        /// </summary>
        /// <param name="contractHash"></param>
        /// <returns></returns>
        Task DeleteContractByHash(UInt160 contractHash);
    }
}