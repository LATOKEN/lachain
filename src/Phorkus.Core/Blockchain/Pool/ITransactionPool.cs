using System.Collections.Generic;
using Phorkus.Core.Proto;

namespace Phorkus.Core.Blockchain.Pool
{
    public interface ITransactionPool
    {
        IReadOnlyDictionary<UInt256, SignedTransaction> Transactions { get; }

        void Add(SignedTransaction transaction);

        IReadOnlyCollection<SignedTransaction> Peek();

        void Delete(UInt256 transactionHash);

        void Clear();
    }
}