using System;
using Phorkus.Core.Cryptography;
using Phorkus.Proto;

namespace Phorkus.Core.Blockchain.OperationManager
{
    public interface ITransactionManager
    {
        event EventHandler<SignedTransaction> OnTransactionPersisted;
        event EventHandler<SignedTransaction> OnTransactionFailed;
        event EventHandler<SignedTransaction> OnTransactionConfirmed;
        event EventHandler<SignedTransaction> OnTransactionSigned;
        
        Transaction GetByHash(UInt256 transactionHash);
        
        OperatingError Persist(SignedTransaction transaction);

        OperatingError Execute(UInt256 txHash);
        
        SignedTransaction Sign(Transaction transaction, KeyPair keyPair);
        
        OperatingError VerifySignature(SignedTransaction transaction, PublicKey publicKey);
        
        OperatingError VerifySignature(SignedTransaction transaction);
        
        OperatingError Verify(Transaction transaction);
    }
}