using System.Runtime.CompilerServices;
using Lachain.Core.Blockchain.Interface;
using Lachain.Crypto;
using Lachain.Crypto.ECDSA;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Core.Blockchain.Operations
{
    public class TransactionSigner : ITransactionSigner
    {
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();

        [MethodImpl(MethodImplOptions.Synchronized)]
        public TransactionReceipt Sign(Transaction transaction, EcdsaKeyPair keyPair)
        {
            /* use raw byte arrays to sign transaction hash */
            var messageHash = transaction.RawHash();
            var signature = Crypto.SignHashed(messageHash.ToBytes(), keyPair.PrivateKey.Encode()).ToSignature();
            var signed = new TransactionReceipt
            {
                Transaction = transaction,
                Hash = transaction.FullHash(signature),
                Signature = signature
            };
            return signed;
        }
    }
}