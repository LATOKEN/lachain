using System.Collections.Generic;
using System.Threading.Tasks;
using NeoSharp.Core.Models;
using NeoSharp.Types;

namespace NeoSharp.Core.Storage.Blockchain
{
    public interface ITransactionRepository
    {
        /// <summary>
        /// Retrieves a transaction by identifier / hash
        /// </summary>
        /// <param name="txHash">Identifier / hash of the transaction</param>
        /// <returns>Transaction with the specified id / hash</returns>
        Task<Transaction> GetTransactionByHash(UInt256 txHash);
        
        /// <summary>
        /// Adds a transaction to the repository
        /// </summary>
        /// <param name="transaction">Transaction to add</param>
        Task AddTransaction(Transaction transaction);

        Task<IEnumerable<Transaction>> GetTransactionsByHashes(IReadOnlyCollection<UInt256> hashes);

        Task<bool> ContainsTransactionByHash(UInt256 txHash);
    }
}