using System.Threading.Tasks;
using NeoSharp.Core.Models;
using NeoSharp.Core.Models.Transcations;
using NeoSharp.Core.Storage.Blockchain;

namespace NeoSharp.Core.Blockchain.Processing.TranscationProcessing
{
    public class PublishTransactionPersister : ITransactionPersister<PublishTransaction>
    {
        private readonly ITransactionRepository _transactionRepository;
        private readonly IContractRepository _contractRepository;

        public PublishTransactionPersister(
            ITransactionRepository transactionRepository,
            IContractRepository contractRepository)
        {
            _transactionRepository = transactionRepository;
            _contractRepository = contractRepository;
        }

        public async Task Persist(PublishTransaction transaction)
        {
            var contract = new Contract
            {
                Code = new Code
                {
                    ScriptHash = transaction.ScriptHash,
                    Script = transaction.Script,
                    ReturnType = transaction.ReturnType,
                    Parameters = transaction.ParameterList
                },
                Name = transaction.Name,
                Version = transaction.CodeVersion,
                Author = transaction.Author,
                Email = transaction.Email,
                Description = transaction.Description
            };

            await _contractRepository.AddContract(contract);
        }
    }
}