using System;
using Phorkus.Proto;

namespace Phorkus.Core.Blockchain
{
    public interface ITransactionVerifier
    {
        event EventHandler<SignedTransaction> OnTransactionVerified;
        
        void VerifyTransaction(SignedTransaction signedTransaction);

        bool VerifyTransactionImmediately(SignedTransaction signedTransaction);
        
        void Start();
        
        void Stop();
    }
}