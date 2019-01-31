using System.Collections.Generic;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Proto;

namespace Phorkus.Core.Blockchain
{
    public interface ITransactionPool
    {
        IReadOnlyDictionary<UInt256, AcceptedTransaction> Transactions { get; }

        void Restore();

        bool Add(Transaction transaction, Signature signature);
        
        bool Add(AcceptedTransaction transaction);
        
        IReadOnlyCollection<AcceptedTransaction> Peek(int limit = -1);

        uint Size();
        
        void Delete(UInt256 transactionHash);

        void Clear();
    }
}