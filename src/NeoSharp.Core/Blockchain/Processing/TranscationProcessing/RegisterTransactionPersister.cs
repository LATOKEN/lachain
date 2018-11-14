using System.Threading.Tasks;
using NeoSharp.Core.Extensions;
using NeoSharp.Core.Models;
using NeoSharp.Core.Models.Transactions;
using NeoSharp.Core.Storage.Blockchain;

namespace NeoSharp.Core.Blockchain.Processing.TranscationProcessing
{
    public class RegisterTransactionPersister : ITransactionPersister<RegisterTransaction>
    {
        private readonly ITransactionRepository _transactionRepository;
        private readonly IAssetRepository _assetRepository;

        public RegisterTransactionPersister(
            ITransactionRepository transactionRepository,
            IAssetRepository assetRepository)
        {
            _transactionRepository = transactionRepository;
            _assetRepository = assetRepository;
        }

        public async Task Persist(RegisterTransaction transaction)
        {
            var asset = new Asset
            {
                Hash = transaction.Hash.ToScriptHash(),
                AssetType = transaction.AssetType,
                Name = transaction.Name,
                Amount = transaction.Supply,
                Precision = transaction.Precision,
                Owner = transaction.Owner
            };
            _assetRepository.AddAsset(asset);
        }
    }
}