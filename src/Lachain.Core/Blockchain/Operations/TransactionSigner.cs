using System.Runtime.CompilerServices;
using Lachain.Core.Blockchain.Interface;
using Lachain.Crypto;
using Lachain.Crypto.ECDSA;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Core.Blockchain.Operations
{
    /*
        This class signs a transaction given ecdsa public and private key
    */
    public class TransactionSigner : ITransactionSigner
    {
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();

        [MethodImpl(MethodImplOptions.Synchronized)]
        public TransactionReceipt Sign(Transaction transaction, EcdsaKeyPair keyPair, bool useNewChainId)
        {
            /* use raw byte arrays to sign transaction hash */
            var messageHash = transaction.RawHash(useNewChainId);
            var signature = Crypto.SignHashed(messageHash.ToBytes(), keyPair.PrivateKey.Encode(), useNewChainId).ToSignature(useNewChainId);
            var signed = new TransactionReceipt
            {
                Transaction = transaction,
                Hash = transaction.FullHash(signature, useNewChainId),
                Signature = signature
            };
            return signed;
        }
    }
}