using System;
using Lachain.Proto;

namespace Lachain.Core.Blockchain.Interface
{
    public interface ITransactionVerifier
    {
        event EventHandler<TransactionReceipt> OnTransactionVerified;
        
        void VerifyTransaction(TransactionReceipt acceptedTransaction, ECDSAPublicKey publicKey);
        void VerifyTransaction(TransactionReceipt acceptedTransaction);

        bool VerifyTransactionImmediately(TransactionReceipt receipt, ECDSAPublicKey publicKey);
        bool VerifyTransactionImmediately(TransactionReceipt receipt, bool cacheEnabled = true);
        
        void Start();
        void Stop();
    }
}