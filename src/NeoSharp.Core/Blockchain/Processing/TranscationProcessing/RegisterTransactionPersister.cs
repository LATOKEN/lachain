using System.Threading.Tasks;
using NeoSharp.Core.Models;
using NeoSharp.Core.Models.Transcations;
using NeoSharp.Core.Storage;

namespace NeoSharp.Core.Blockchain.Processing.TranscationProcessing
{
    public class RegisterTransactionPersister : ITransactionPersister<RegisterTransaction>
    {
        private readonly IRepository _repository;

        public RegisterTransactionPersister(IRepository repository)
        {
            _repository = repository;
        }

        public async Task Persist(RegisterTransaction transaction)
        {
            await _repository.AddAsset(new Asset
            {
                Hash = transaction.Hash,
                AssetType = transaction.AssetType,
                Name = transaction.Name,
                Amount = transaction.Supply,
                Precision = transaction.Precision,
                Owner = transaction.Owner
            });
        }
    }
}