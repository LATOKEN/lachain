using Phorkus.Crypto;
using Phorkus.Proto;

namespace Phorkus.Core.Blockchain
{
    public interface ITransactionSigner
    {
        TransactionReceipt Sign(Transaction transaction, ECDSAKeyPair keyPair);
    }
}