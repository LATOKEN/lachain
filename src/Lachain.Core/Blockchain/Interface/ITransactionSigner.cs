using Lachain.Crypto;
using Lachain.Proto;

namespace Lachain.Core.Blockchain.Interface
{
    public interface ITransactionSigner
    {
        TransactionReceipt Sign(Transaction transaction, ECDSAKeyPair keyPair);
    }
}