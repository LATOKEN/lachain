using Phorkus.Proto;

namespace Phorkus.Storage.State
{
    public interface IContractSnapshot : ISnapshot
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
        /// <param name="from"></param>
        /// <param name="contract"></param>
        /// <returns></returns>
        void AddContract(UInt160 from, Contract contract);

        /// <summary>
        /// Delete a smart contract by its hash
        /// </summary>
        /// <param name="contractHash"></param>
        /// <returns></returns>
        void DeleteContractByHash(UInt160 contractHash);

        uint GetTotalContractsByFrom(UInt160 from);
    }
}