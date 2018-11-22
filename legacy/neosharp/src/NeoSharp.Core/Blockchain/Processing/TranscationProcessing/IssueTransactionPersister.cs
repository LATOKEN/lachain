using System.Threading.Tasks;
using NeoSharp.Core.Models.Transactions;
using NeoSharp.Core.Storage;
using NeoSharp.Core.Storage.Blockchain;

namespace NeoSharp.Core.Blockchain.Processing.TranscationProcessing
{
    public class IssueTransactionPersister : ITransactionPersister<IssueTransaction>
    {
        private readonly ITransactionRepository _transactionRepository;

        public IssueTransactionPersister(ITransactionRepository transactionRepository)
        {
            _transactionRepository = transactionRepository;
        }

        public async Task Persist(IssueTransaction transaction)
        {
            /* TODO: "implement issue tx persister" */
            
            /*var inputsTasks = transaction.Inputs
                .Select(async coin => (await _repository.GetTransaction(coin.PrevHash)).Outputs[coin.PrevIndex]);

            var inputs = await Task.WhenAll(inputsTasks);

            var assetChanges = new Dictionary<UInt256, Fixed8>();

            foreach (var input in inputs)
            {
                if (assetChanges.ContainsKey(input.AssetId))
                    assetChanges[input.AssetId] -= input.Value;
                else
                    assetChanges[input.AssetId] = -input.Value;
            }

            foreach (var output in transaction.Outputs)
            {
                if (assetChanges.ContainsKey(output.AssetId))
                    assetChanges[output.AssetId] += output.Value;
                else
                    assetChanges[output.AssetId] = output.Value;
            }

            foreach (var assetChange in assetChanges.Where(ach => ach.Value != Fixed8.Zero))
            {
                var asset = await _repository.GetAsset(assetChange.Key);

                asset.Available += assetChange.Value;

                await _repository.AddAsset(asset);
            }*/
        }
    }
}