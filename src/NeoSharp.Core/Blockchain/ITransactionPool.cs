using System.Collections.Generic;
using NeoSharp.Core.Models;
using NeoSharp.Types;

namespace NeoSharp.Core.Blockchain
{
    public interface ITransactionPool
    {
        int Capacity { get; }

        int Size { get; }

        IEnumerable<Transaction> GetTransactions();
        
        bool Contains(UInt256 hash);
        
        void Add(Transaction transaction);

        void Remove(UInt256 hash);
    }
}