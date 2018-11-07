using System;
using System.Linq;
using System.Threading.Tasks;
using NeoSharp.Core.Blockchain.State;
using NeoSharp.Core.Models;
using NeoSharp.Core.Persistence;
using NeoSharp.Types;

namespace NeoSharp.Core.Blockchain.Processing
{
    /// <summary>
    ///     Processes the common properties of a Transaction.
    ///     Processes the outputs by creating the coin states for them and updating balances.
    ///     Processes the inputs by marking their state with CoinState.Spent and updating balances.
    ///     Calls the respective transaction type processors for further processing.
    /// </summary>
    public class TransactionPersister : ITransactionPersister<Transaction>
    {
        private readonly IRepository _repository;
        private readonly IAccountManager _accountManager;
        private readonly ITransactionPersister<IssueTransaction> _issueTransactionPersister;
        private readonly ITransactionPersister<RegisterTransaction> _registerTransactionPersister;
        private readonly ITransactionPersister<PublishTransaction> _publishTransactionPersister;
        private readonly ITransactionPool _transactionPool;

        public TransactionPersister(
            IRepository repository,
            IAccountManager accountManager,
            ITransactionPersister<IssueTransaction> issueTransactionPersister,
            ITransactionPersister<RegisterTransaction> registerTransactionPersister,
            ITransactionPersister<PublishTransaction> publishTransactionPersister,
            ITransactionPool transactionPool
        )
        {
            _repository = repository;
            _accountManager = accountManager;
            _issueTransactionPersister = issueTransactionPersister;
            _registerTransactionPersister = registerTransactionPersister;
            _publishTransactionPersister = publishTransactionPersister;
            _transactionPool = transactionPool;
        }

        public async Task Persist(Transaction transaction)
        {
            await SpendOutputs(transaction.Inputs);
            await GainOutputs(transaction.Hash, transaction.Outputs);

            switch (transaction)
            {
                case ContractTransaction _:
                case MinerTransaction _:
                    break;
                case IssueTransaction issue:
                    await _issueTransactionPersister.Persist(issue);
                    break;
                case PublishTransaction publish:
                    await _publishTransactionPersister.Persist(publish);
                    break;
                case RegisterTransaction register:
                    await _registerTransactionPersister.Persist(register);
                    break;
                default:
                    throw new ArgumentException("Unknown Transaction Type");
            }

            await _repository.AddTransaction(transaction);
            _transactionPool.Remove(transaction.Hash);
        }

        private async Task GainOutputs(UInt256 hash, TransactionOutput[] outputs)
        {
            foreach (var output in outputs)
                await _accountManager.UpdateBalance(output.ScriptHash, output.AssetId, output.Value);

            var newCoinStates = outputs.Select(o => CoinState.New).ToArray();

            await _repository.AddCoinStates(hash, newCoinStates);
        }

        private async Task SpendOutputs(CoinReference[] inputs)
        {
            foreach (var inputGroup in inputs.GroupBy(i => i.PrevHash))
            {
                var prevHash = inputGroup.Key;
                var transaction = await _repository.GetTransaction(prevHash);
                var coinStates = await _repository.GetCoinStates(prevHash);

                foreach (var coinReference in inputGroup)
                {
                    coinStates[coinReference.PrevIndex] |= CoinState.Spent;

                    var output = transaction.Outputs[coinReference.PrevIndex];
                    await _accountManager.UpdateBalance(output.ScriptHash, output.AssetId, -output.Value);
                }

                await _repository.AddCoinStates(prevHash, coinStates);
            }
        }
    }
}