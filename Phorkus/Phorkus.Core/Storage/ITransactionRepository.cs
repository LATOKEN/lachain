using System.Collections.Generic;
using Phorkus.Core.Proto;

namespace Phorkus.Core.Storage
{
    public interface ITransactionRepository
    {
        /// <summary>
        /// Retrieves a transaction by identifier / hash
        /// </summary>
        /// <param name="txHash">Identifier / hash of the transaction</param>
        /// <returns>Transaction with the specified id / hash</returns>
        Transaction GetTransactionByHash(UInt256 txHash);
        
        /// <summary>
        /// Adds a transaction to the repository
        /// </summary>
        /// <param name="transaction">Transaction to add</param>
        void AddTransaction(Transaction transaction);

        IEnumerable<Transaction> GetTransactionsByHashes(IReadOnlyCollection<UInt256> hashes);

        bool ContainsTransactionByHash(UInt256 txHash);

        uint GetTotalTransactionCount(UInt160 address);
    }
}