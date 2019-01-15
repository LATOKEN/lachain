using System;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Proto;

namespace Phorkus.Core.Blockchain
{
    public interface ITransactionVerifier
    {
        event EventHandler<SignedTransaction> OnTransactionVerified;
        
        void VerifyTransaction(SignedTransaction signedTransaction, PublicKey publicKey);
        void VerifyTransaction(SignedTransaction signedTransaction);

        bool VerifyTransactionImmediately(SignedTransaction transaction, PublicKey publicKey);
        bool VerifyTransactionImmediately(SignedTransaction transaction);
        
        void Start();
        void Stop();
    }
}