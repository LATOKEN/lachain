using System.Collections.Concurrent;
using NeoSharp.Types;

namespace NeoSharp.Core.Network
{
    public interface ITransactionCrawler : IServerProcess
    {
        ConcurrentQueue<UInt256> PendingTransactionsHashes { get; }
        void AddTransactionHash(UInt256 hash);
    }
}