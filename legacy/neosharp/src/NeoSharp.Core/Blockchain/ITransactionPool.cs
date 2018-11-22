using System.Collections.Generic;
using NeoSharp.Core.Models;

namespace NeoSharp.Core.Blockchain
{
    public interface ITransactionPool
    {
        int Capacity { get; }

        int Size { get; }

        IEnumerable<Transaction> GetTransactions();
        
        void Add(Transaction transaction);

        void Remove(UInt256 hash);

        bool Contains(UInt256 hash);

        Transaction FindByHash(UInt256 hash);
    }
}