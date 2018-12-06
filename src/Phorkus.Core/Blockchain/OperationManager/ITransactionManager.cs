using System;
using Phorkus.Core.Blockchain.State;
using Phorkus.Proto;

namespace Phorkus.Core.Blockchain.OperationManager
{
    public interface ITransactionManager : ITransactionSigner
    {
        event EventHandler<SignedTransaction> OnTransactionPersisted;
        event EventHandler<SignedTransaction> OnTransactionFailed;
        event EventHandler<SignedTransaction> OnTransactionExecuted;
        event EventHandler<SignedTransaction> OnTransactionSigned;
        
        SignedTransaction GetByHash(UInt256 transactionHash);
        
        OperatingError Persist(SignedTransaction transaction);

        OperatingError Execute(Block block, UInt256 txHash, IBlockchainSnapshot snapshot);
        
        OperatingError Verify(Transaction transaction);

        uint CalcNextTxNonce(UInt160 from);
    }
}