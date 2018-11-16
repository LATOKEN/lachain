using System;
using Phorkus.Core.Proto;

namespace Phorkus.Core.Blockchain.OperationManager
{
    public interface ITransactionOperationManager
    {
        event EventHandler<Transaction> OnTransactionPersisted;
        event EventHandler<Transaction> OnTransactionSigned;
        
        bool Verify(BlockHeader blockHeader);
    }
}