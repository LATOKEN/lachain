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
        bool ContainsTransactionByHash(UInt256 txHash);
        /// <summary>
        /// Removes the list of transaction hashes from pool
        /// </summary>
        /// <param name="txHashes"></param>
        /// <returns>The number of transactions removed</returns>
        int RemoveTransactions(IEnumerable<UInt256> txHashes);
        /// <summary>
        /// Tries to add transaction <c>txToAdd</c> and remove transaction <c>txToRemove</c>
        /// from DB
        /// </summary>
        /// <param name="txToAdd">Transaction receipt to add in DB</param>
        /// <param name="txToRemove">Transaction receipt to remove from DB</param>
        void AddAndRemoveTx(TransactionReceipt txToAdd, TransactionReceipt txToRemove);

        void ClearPool();
    }
}