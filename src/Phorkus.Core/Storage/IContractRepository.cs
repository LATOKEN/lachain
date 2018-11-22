using Phorkus.Core.Proto;

namespace Phorkus.Core.Storage
{
    public interface IContractRepository
    {
        /// <summary>
        /// Retrieves a smart contract by its hash
        /// </summary>
        /// <param name="contractHash"></param>
        /// <returns></returns>
        Contract GetContractByHash(UInt160 contractHash);
        
        /// <summary>
        /// Adds a smart contract
        /// </summary>
        /// <param name="contract"></param>
        /// <returns></returns>
        void AddContract(Contract contract);

        /// <summary>
        /// Delete a smart contract by its hash
        /// </summary>
        /// <param name="contractHash"></param>
        /// <returns></returns>
        void DeleteContractByHash(UInt160 contractHash);
    }
}