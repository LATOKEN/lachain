using System.Collections.Generic;
using Phorkus.Proto;

namespace Phorkus.Storage.Repositories
{
    public interface ITransactionRepository
    {
        /// <summary>
        /// Retrieves a transaction by identifier / hash
        /// </summary>
        /// <param name="txHash">Identifier / hash of the transaction</param>
        /// <returns>Transaction with the specified id / hash</returns>
        SignedTransaction GetTransactionByHash(UInt256 txHash);
        
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
        bool RemoveFromTransactionFromPool(UInt256 txHash);
        
        /// <summary>
        /// Adds a transaction to the repository
        /// </summary>
        /// <param name="transaction">Transaction to add</param>
        void AddTransaction(SignedTransaction transaction);

        TransactionState ChangeTransactionState(UInt256 txHash, TransactionState txState);

        TransactionState GetTransactionState(UInt256 txHash);
        
        IEnumerable<SignedTransaction> GetTransactionsByHashes(IEnumerable<UInt256> txHashes);

        bool ContainsTransactionByHash(UInt256 txHash);
        
        SignedTransaction GetLatestTransactionByFrom(UInt160 from);

        void CommitTransaction(SignedTransaction signedTransaction, UInt256 blockHash);
        
        /// <summary>
        /// TODO: "replace this function with state manager"
        /// </summary>
        /// <param name="from"></param>
        /// <returns></returns>
        uint GetTotalTransactionCount(UInt160 from);
    }
}