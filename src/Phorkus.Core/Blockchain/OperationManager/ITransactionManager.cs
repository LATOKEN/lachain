using System;
using Phorkus.Proto;
using Phorkus.Storage.State;

namespace Phorkus.Core.Blockchain.OperationManager
{
    public interface ITransactionManager : ITransactionSigner
    {
        event EventHandler<AcceptedTransaction> OnTransactionPersisted;
        event EventHandler<AcceptedTransaction> OnTransactionFailed;
        event EventHandler<AcceptedTransaction> OnTransactionExecuted;
        event EventHandler<AcceptedTransaction> OnTransactionSigned;
        
        AcceptedTransaction GetByHash(UInt256 transactionHash);

        OperatingError Execute(Block block, AcceptedTransaction transaction, IBlockchainSnapshot snapshot);
        
        OperatingError Verify(AcceptedTransaction transaction);

        OperatingError VerifySignature(AcceptedTransaction transaction, PublicKey publicKey);
        
        OperatingError VerifySignature(AcceptedTransaction transaction);
        
        ulong CalcNextTxNonce(UInt160 from);
    }
}