using System.Collections.Generic;
using Phorkus.Proto;

namespace Phorkus.Core.Blockchain
{
    public interface ITransactionPool
    {
        IReadOnlyDictionary<UInt256, SignedTransaction> Transactions { get; }

        void Restore();
        
        bool Add(SignedTransaction transaction);
        
        IReadOnlyCollection<SignedTransaction> Peek(int limit = -1);

        uint Size();
        
        void Delete(UInt256 transactionHash);

        void Clear();
    }
}