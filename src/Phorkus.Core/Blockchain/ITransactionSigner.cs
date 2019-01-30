using Phorkus.Crypto;
using Phorkus.Proto;

namespace Phorkus.Core.Blockchain
{
    public interface ITransactionSigner
    {
        AcceptedTransaction Sign(Transaction transaction, KeyPair keyPair);
    }
}