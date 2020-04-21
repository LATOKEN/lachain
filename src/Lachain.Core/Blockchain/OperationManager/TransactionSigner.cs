using System.Runtime.CompilerServices;
using Lachain.Core.Blockchain.Interface;
using Lachain.Crypto;
using Lachain.Crypto.ECDSA;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Core.Blockchain.OperationManager
{
    public class TransactionSigner : ITransactionSigner
    {
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public TransactionReceipt Sign(Transaction transaction, EcdsaKeyPair keyPair)
        {
            /* use raw byte arrays to sign transaction hash */
            var messageHash = HashUtils.ToHash256(transaction);
            var signature = Crypto.SignHashed(messageHash.ToBytes(), keyPair.PrivateKey.Encode());
            var signed = new TransactionReceipt
            {
                Transaction = transaction,
                Hash = messageHash,
                Signature = signature.ToSignature()
            };
            return signed;
        }
    }
}