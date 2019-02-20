using System;
using Phorkus.Proto;
using Phorkus.Storage.State;

namespace Phorkus.Core.Blockchain.OperationManager
{
    public interface ITransactionManager : ITransactionSigner
    {
        event EventHandler<TransactionReceipt> OnTransactionPersisted;
        event EventHandler<TransactionReceipt> OnTransactionFailed;
        event EventHandler<TransactionReceipt> OnTransactionExecuted;
        event EventHandler<TransactionReceipt> OnTransactionSigned;
        
        TransactionReceipt GetByHash(UInt256 transactionHash);

        OperatingError Execute(Block block, TransactionReceipt transaction, IBlockchainSnapshot snapshot);
        
        OperatingError Verify(TransactionReceipt transaction);

        OperatingError VerifySignature(TransactionReceipt transaction, PublicKey publicKey);
        
        OperatingError VerifySignature(TransactionReceipt transaction, bool cacheEnabled = true);
        
        ulong CalcNextTxNonce(UInt160 from);
    }
}