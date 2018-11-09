using System.Collections.Generic;
using System.Threading.Tasks;
using NeoSharp.Core.Models;
using NeoSharp.Core.Persistence;
using NeoSharp.Types;

namespace NeoSharp.Core.Blockchain.Repositories
{
    public class TransactionRepository : ITransactionRepository
    {
        #region Private Fields 
        private readonly IRepository _repository;
        #endregion

        #region Construtor 
        public TransactionRepository(IRepository repository)
        {
            _repository = repository;
        }
        #endregion

        #region ITransactionModel Implementation 

        /// <inheritdoc />
        public async Task<Transaction> GetTransaction(UInt256 hash)
        {
            return await _repository.GetTransaction(hash);
        }

        public async Task<bool> ContainsTransaction(UInt256 hash)
        {
            // TODO #389: Optimize this
            return await GetTransaction(hash) != null;
        }

        public async Task<IEnumerable<Transaction>> GetTransactions(IReadOnlyCollection<UInt256> transactionHashes)
        {
            var transactions = new List<Transaction>();

            foreach (var hash in transactionHashes)
            {
                var transaction = await GetTransaction(hash);
                if (transaction == null)
                    continue;
                transactions.Add(transaction);
            }

            return transactions;
        }

        /// <inheritdoc />
        public bool IsDoubleSpend(Transaction transaction)
        {
            return false;
            
            /*if (transaction.Inputs.Length == 0)
                return false;
            foreach (var group in transaction.Inputs.GroupBy(p => p.PrevHash))
            {
                var states = _repository.GetCoinStates(group.Key).Result;
                if (states == null || group.Any(p => p.PrevIndex >= states.Length || states[p.PrevIndex].HasFlag(CoinState.Spent)))
                    return true;
            }
            return false;*/
        }

        #endregion
    }
}