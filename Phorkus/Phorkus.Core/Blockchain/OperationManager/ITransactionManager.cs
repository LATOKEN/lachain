using System;
using Phorkus.Core.Cryptography;
using Phorkus.Core.Proto;

namespace Phorkus.Core.Blockchain.OperationManager
{
    public interface ITransactionManager
    {
        event EventHandler<SignedTransaction> OnTransactionPersisted;
        event EventHandler<SignedTransaction> OnTransactionFailed;
        event EventHandler<SignedTransaction> OnTransactionSigned;
        
        HashedTransaction GetByHash(UInt256 transactionHash);
        
        OperatingError Persist(SignedTransaction transaction);
        
        SignedTransaction Sign(HashedTransaction transaction, KeyPair keyPair);
        
        OperatingError VerifySignature(SignedTransaction transaction, PublicKey publicKey);
        
        OperatingError Verify(HashedTransaction transaction);
    }
}