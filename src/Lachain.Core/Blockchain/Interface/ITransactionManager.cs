using System;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.VM;
using Lachain.Proto;
using Lachain.Storage.State;

namespace Lachain.Core.Blockchain.Interface
{
    public interface ITransactionManager
    {
        event EventHandler<TransactionReceipt>? OnTransactionFailed;
        event EventHandler<TransactionReceipt>? OnTransactionExecuted;
        event EventHandler<InvocationContext>? OnSystemContractInvoked;

        TransactionReceipt? GetByHash(UInt256 transactionHash);

        OperatingError Execute(Block block, TransactionReceipt receipt, IBlockchainSnapshot snapshot);

        OperatingError Verify(TransactionReceipt transaction, bool useNewChainId);

        OperatingError VerifySignature(TransactionReceipt transaction, ECDSAPublicKey publicKey, bool useNewChainId);

        OperatingError VerifySignature(TransactionReceipt transaction, bool useNewChainId, bool cacheEnabled);

        ulong CalcNextTxNonce(UInt160 from);
    }
}