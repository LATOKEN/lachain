using System;
using System.Collections.Generic;
using Lachain.Proto;

namespace Lachain.Core.Blockchain.Interface
{
    public interface ITransactionVerifier
    {
        event EventHandler<(TransactionReceipt, TransactionStatus)>? OnVerificationCompleted;
        event EventHandler<object?>? OnVerificationStarted;

        void ClearQueue();
        
        void VerifyTransaction(TransactionReceipt acceptedTransaction, ECDSAPublicKey publicKey, bool useNewChainId);
        void VerifyTransaction(TransactionReceipt acceptedTransaction,  bool useNewChainId);
        void VerifyTransactions(IReadOnlyCollection<TransactionReceipt> acceptedTransactions, bool useNewChainId);

        bool VerifyTransactionImmediately(TransactionReceipt receipt, ECDSAPublicKey publicKey,  bool useNewChainId);
        bool VerifyTransactionImmediately(TransactionReceipt receipt, bool useNewChainId, bool cacheEnabled);
        
        void Start();
        void Stop();
    }
}