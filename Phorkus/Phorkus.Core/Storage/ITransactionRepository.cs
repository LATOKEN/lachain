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
        SignedTransaction GetTransactionByHash(UInt256 txHash);
        
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
        
        uint GetTotalTransactionCount(UInt160 from);
    }
}