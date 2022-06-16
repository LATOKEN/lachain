using System.Collections.Generic;
using Lachain.Proto;

namespace Lachain.Storage.Repositories
{
    public interface IPoolRepository
    {
        /// <summary>
        /// Retrieves a transaction by identifier / hash
        /// </summary>
        /// <param name="txHash">Identifier / hash of the transaction</param>
        /// <returns>Transaction with the specified id / hash</returns>
        TransactionReceipt? GetTransactionByHash(UInt256 txHash);
        
        /// <summary>
        /// Adds a transaction to the repository
        /// </summary>
        /// <param name="transaction">Transaction to add</param>
        void AddTransaction(TransactionReceipt transaction);
        
        /// <summary>
        /// Returns transaction hashes that hasn't been committed to block
        /// </summary>
        /// <returns></returns>
        ICollection<UInt256> GetTransactionPool();
        
        /// <summary>
        /// Removes transaction hash from pool
        /// </summary>
        /// <param name="txHash"></param>
        /// <returns></returns>
        bool RemoveTransaction(UInt256 txHash);
        
        /// <summary>
        /// Check does transaction by hash exists in pool or something else
        /// </summary>
        /// <param name="txHash"></param>
        /// <returns></returns>

        int RemoveTransactions(IEnumerable<UInt256> txHashes);

        bool ContainsTransactionByHash(UInt256 txHash);

        void ClearPool();
    }
}