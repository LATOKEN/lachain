﻿using System.Collections.Generic;
using System.Threading.Tasks;
using NeoSharp.Core.Models;
using NeoSharp.Types;

namespace NeoSharp.Core.Blockchain.Repositories
{
    public interface ITransactionRepository
    {
        /// <summary>
        /// Returns the corresponding transaction information according to the specified hash value
        /// </summary>
        /// <param name="hash">Transaction hash.</param>
        /// <returns>The transaction.</returns>
        Task<Transaction> GetTransaction(UInt256 hash);

        /// <summary>
        /// Determine whether the specified transaction is included in the blockchain
        /// </summary>
        /// <param name="hash">Transaction hash</param>
        /// <returns>Return true if the specified transaction is included</returns>
        Task<bool> ContainsTransaction(UInt256 hash);

        Task<IEnumerable<Transaction>> GetTransactions(IReadOnlyCollection<UInt256> transactionHashes);

        /// <summary>
        /// Determine if the transaction is double
        /// </summary>
        /// <param name="transaction">The transaction.</param>
        /// <returns>Boolean value indicating if is a double spend or not.</returns>
        bool IsDoubleSpend(Transaction transaction);
    }
}
