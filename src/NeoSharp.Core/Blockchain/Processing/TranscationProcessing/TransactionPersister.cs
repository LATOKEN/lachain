using System;
using System.Threading.Tasks;
using NeoSharp.Core.Models;
using NeoSharp.Core.Models.Transactions;
using NeoSharp.Core.Storage.Blockchain;

namespace NeoSharp.Core.Blockchain.Processing.TranscationProcessing
{
    /// <summary>
    ///     Processes the common properties of a Transaction.
    ///     Processes the outputs by creating the coin states for them and updating balances.
    ///     Processes the inputs by marking their state with CoinState.Spent and updating balances.
    ///     Calls the respective transaction type processors for further processing.
    /// </summary>
    public class TransactionPersister : ITransactionPersister<Transaction>
    {
        private readonly ITransactionPersister<ContractTransaction> _contractTransactionPersister;
        private readonly ITransactionPersister<IssueTransaction> _issueTransactionPersister;
        private readonly ITransactionPersister<RegisterTransaction> _registerTransactionPersister;
        private readonly ITransactionPersister<PublishTransaction> _publishTransactionPersister;
        private readonly ITransactionRepository _transactionRepository;
        private readonly ITransactionPool _transactionPool;

        public TransactionPersister(
            ITransactionPersister<ContractTransaction> contractTransactionPersister,
            ITransactionPersister<IssueTransaction> issueTransactionPersister,
            ITransactionPersister<RegisterTransaction> registerTransactionPersister,
            ITransactionPersister<PublishTransaction> publishTransactionPersister,
            ITransactionRepository transactionRepository,
            ITransactionPool transactionPool)
        {
            _contractTransactionPersister = contractTransactionPersister;
            _issueTransactionPersister = issueTransactionPersister;
            _registerTransactionPersister = registerTransactionPersister;
            _publishTransactionPersister = publishTransactionPersister;
            _transactionRepository = transactionRepository;
            _transactionPool = transactionPool;
        }

        public async Task Persist(Transaction transaction)
        {
            /*await SpendOutputs(transaction.Inputs);
            await GainOutputs(transaction.Hash, transaction.Outputs);*/

            switch (transaction)
            {
                case MinerTransaction _:
                    break;
                case ContractTransaction contract:
                    await _contractTransactionPersister.Persist(contract);
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

            await _transactionRepository.AddTransaction(transaction);
            _transactionPool.Remove(transaction.Hash);
        }

        /*private async Task GainOutputs(UInt256 hash, TransactionOutput[] outputs)
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
                if (transaction == null)
                    continue;
                var coinStates = await _repository.GetCoinStates(prevHash);
                if (coinStates == null)
                    continue;

                foreach (var coinReference in inputGroup)
                {
                    coinStates[coinReference.PrevIndex] |= CoinState.Spent;

                    var output = transaction.Outputs[coinReference.PrevIndex];
                    await _accountManager.UpdateBalance(output.ScriptHash, output.AssetId, -output.Value);
                }

                await _repository.AddCoinStates(prevHash, coinStates);
            }
        }*/
    }
}