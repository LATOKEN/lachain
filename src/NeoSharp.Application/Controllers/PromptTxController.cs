using System.IO;
using System.Linq;
using NeoSharp.Application.Attributes;
using NeoSharp.Application.Client;
using NeoSharp.BinarySerialization;
using NeoSharp.Core.Blockchain.Repositories;
using NeoSharp.Core.Models;
using NeoSharp.Core.Models.OperationManger;
using NeoSharp.Cryptography;
using NeoSharp.Types;
using NeoSharp.Types.ExtensionMethods;

namespace NeoSharp.Application.Controllers
{
    public class PromptTxController : IPromptController
    {
        private readonly ITransactionRepository _transactionRepository;
        private readonly IStateRepository _stateRepository;
        private readonly ISigner<Transaction> _transactionSigner;
        private readonly IConsoleHandler _consoleHandler;
        private readonly IBinarySerializer _binarySerializer;

        public PromptTxController(
            ITransactionRepository transactionRepository,
            IStateRepository stateRepository,
            ISigner<Transaction> transactionSigner,
            IConsoleHandler consoleHandler,
            IBinarySerializer binarySerializer
        ) {
            _transactionRepository = transactionRepository;
            _stateRepository = stateRepository;
            _transactionSigner = transactionSigner;
            _consoleHandler = consoleHandler;
            _binarySerializer = binarySerializer;
        }
        
        [PromptCommand("tx sign", Category = "Tx", Help = "Sign transaction")]
        public async void signTx(string asset, string from, string to, decimal value, string privateKey = null)
        {
            var inputs = await _stateRepository.GetUnspent(from.ToScriptHash());
            
            var output = new TransactionOutput
            {
                AssetId = UInt256.Parse(asset),
                Value = Fixed8.FromDecimal(value),
                ScriptHash = to.ToScriptHash()
            };
            var tx = new ContractTransaction
            {
                Attributes = new TransactionAttribute[0],
                Inputs = inputs.ToArray(),
                Outputs = new[]
                {
                    output
                }
            };
            _transactionSigner.Sign(tx);
            
            var signature = Crypto.Default.Sign(txToSign(tx), privateKey.HexToBytes());
            
//            tx.Witness;
        }

        private byte[] txToSign(Transaction tx)
        {
            using (var memoryStream = new MemoryStream())            
            using (var binaryWriter = new BinaryWriter(memoryStream))
            {
                var settings = new BinarySerializerSettings
                {
                    Filter = a => a != nameof(tx.Witness)
                };
                tx.Serialize(_binarySerializer, binaryWriter, settings);
                binaryWriter.Flush();
                return memoryStream.ToArray();
            }
        }
        
        [PromptCommand("tx send", Category = "Wallet", Help = "Create a new wallet")]
        void sendTx()
        {
        }
    }
}