using System;
using Lachain.Proto;

namespace Lachain.Core.Blockchain.Interface
{
    public interface ITransactionVerifier
    {
        event EventHandler<TransactionReceipt> OnTransactionVerified;
        
        void VerifyTransaction(TransactionReceipt acceptedTransaction, ECDSAPublicKey publicKey, bool useNewChainId);
        void VerifyTransaction(TransactionReceipt acceptedTransaction,  bool useNewChainId);

        bool VerifyTransactionImmediately(TransactionReceipt receipt, ECDSAPublicKey publicKey,  bool useNewChainId);
        bool VerifyTransactionImmediately(TransactionReceipt receipt, bool useNewChainId, bool cacheEnabled);
        
        void Start();
        void Stop();
    }
}