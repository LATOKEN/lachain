using System;
using System.IO;
using System.Threading.Tasks;
using NeoSharp.BinarySerialization;
using NeoSharp.Core.Cryptography;
using NeoSharp.Core.Models;
using NeoSharp.Core.Models.OperationManager;
using NeoSharp.Core.Models.Transactions;
using NeoSharp.Core.Storage.Blockchain;
using NeoSharp.Cryptography;

namespace NeoSharp.Core.Blockchain
{
    public class TransactionManager : ITransactionManager
    {
        private readonly ITransactionRepository _transactionRepository;
        private readonly IBinarySerializer _binarySerializer;
        private readonly ISigner<Transaction> _transactionSigner;

        public TransactionManager(
            ITransactionRepository transactionRepository,
            IBinarySerializer binarySerializer,
            ISigner<Transaction> transactionSigner)
        {
            _transactionRepository = transactionRepository;
            _binarySerializer = binarySerializer;
            _transactionSigner = transactionSigner;
        }
        
        public async Task<Transaction> CreateContractTransaction(UInt160 asset, UInt160 from, UInt160 to, UInt256 value)
        {
            var nonce = _transactionRepository.GetTotalTransactionCount(from);
            var tx = new ContractTransaction
            {
                Nonce = nonce,
                From = from,
                Asset = asset,
                To = to,
                Value = value
            };
            _transactionSigner.Sign(tx);
            return tx;
        }
        
        public async Task<Signature> SignTransaction(Transaction transaction, KeyPair privateKey)
        {
            var message = _serializeTx(transaction);
            var signature = Crypto.Default.Sign(message, privateKey.PrivateKey);
            var publicKey = Crypto.Default.RecoverSignature(signature, message, true);
            if (!Crypto.Default.VerifySignature(message, signature, publicKey))
                throw new Exception("Unable to verify signature with recovered public key");
            return new Signature(signature);
        }
        
        private byte[] _serializeTx(Transaction transaction)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                transaction.Serialize(_binarySerializer, writer);
                writer.Flush();
                return stream.ToArray();
            }
        }
    }
}