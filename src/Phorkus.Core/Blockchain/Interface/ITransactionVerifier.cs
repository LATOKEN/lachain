using System;
using Phorkus.Proto;

namespace Phorkus.Core.Blockchain.Interface
{
    public interface ITransactionVerifier
    {
        event EventHandler<TransactionReceipt> OnTransactionVerified;
        
        void VerifyTransaction(TransactionReceipt acceptedTransaction, ECDSAPublicKey publicKey);
        void VerifyTransaction(TransactionReceipt acceptedTransaction);

        bool VerifyTransactionImmediately(TransactionReceipt transaction, ECDSAPublicKey publicKey);
        bool VerifyTransactionImmediately(TransactionReceipt transaction, bool cacheEnabled = true);
        
        void Start();
        void Stop();
    }
}