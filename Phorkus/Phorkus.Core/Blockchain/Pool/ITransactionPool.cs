using System.Collections.Generic;
using Phorkus.Core.Proto;

namespace Phorkus.Core.Blockchain.Pool
{
    public interface ITransactionPool
    {
        IReadOnlyDictionary<UInt256, HashedTransaction> Transactions { get; }

        void Add(HashedTransaction transaction);

        IReadOnlyCollection<HashedTransaction> Peek();

        void Delete(UInt256 transactionHash);

        void Clear();
    }
}