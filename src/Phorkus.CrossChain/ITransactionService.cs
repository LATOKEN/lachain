using System.Collections.Generic;
using Phorkus.Proto;

namespace Phorkus.CrossChain
{
    public interface ITransactionService
    {
        AddressFormat AddressFormat { get; }
        
        ulong BlockGenerationTime { get; }
        
        ulong CurrentBlockHeight { get; }

        IEnumerable<IContractTransaction> GetTransactionsAtBlock(byte[] recipient, ulong blockHeight);
        
        byte[] BroadcastTransaction(RawTransaction rawTransaction);

        byte[] GenerateAddress(PublicKey publicKey);

        bool CheckTransactionIsConfirmed(byte[] txHash);
        
        ulong TxConfirmation { get; }
    }
}