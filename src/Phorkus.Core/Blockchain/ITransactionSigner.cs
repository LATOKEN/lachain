using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Cryptography;
using Phorkus.Proto;

namespace Phorkus.Core.Blockchain
{
    public interface ITransactionSigner
    {
        SignedTransaction Sign(Transaction transaction, KeyPair keyPair);
        
        OperatingError VerifySignature(SignedTransaction transaction, PublicKey publicKey);
        
        OperatingError VerifySignature(SignedTransaction transaction);
    }
}