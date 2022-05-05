using Lachain.Crypto.ECDSA;
using Lachain.Proto;

namespace Lachain.Core.Blockchain.Interface
{
    public interface ITransactionSigner
    {
        TransactionReceipt Sign(Transaction transaction, EcdsaKeyPair keyPair, bool useNewChainId);
    }
}