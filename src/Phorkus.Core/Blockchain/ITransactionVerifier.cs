using System;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Proto;

namespace Phorkus.Core.Blockchain
{
    public interface ITransactionVerifier
    {
        event EventHandler<TransactionReceipt> OnTransactionVerified;
        
        void VerifyTransaction(TransactionReceipt acceptedTransaction, PublicKey publicKey);
        void VerifyTransaction(TransactionReceipt acceptedTransaction);

        bool VerifyTransactionImmediately(TransactionReceipt transaction, PublicKey publicKey);
        bool VerifyTransactionImmediately(TransactionReceipt transaction, bool cacheEnabled = true);
        
        void Start();
        void Stop();
    }
}