using System;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Proto;

namespace Phorkus.Core.Blockchain
{
    public interface ITransactionVerifier
    {
        event EventHandler<AcceptedTransaction> OnTransactionVerified;
        
        void VerifyTransaction(AcceptedTransaction acceptedTransaction, PublicKey publicKey);
        void VerifyTransaction(AcceptedTransaction acceptedTransaction);

        bool VerifyTransactionImmediately(AcceptedTransaction transaction, PublicKey publicKey);
        bool VerifyTransactionImmediately(AcceptedTransaction transaction, bool cacheEnabled = true);
        
        void Start();
        void Stop();
    }
}