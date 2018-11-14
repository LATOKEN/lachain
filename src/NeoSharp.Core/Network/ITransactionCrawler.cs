using System.Collections.Concurrent;

namespace NeoSharp.Core.Network
{
    public interface ITransactionCrawler : IServerProcess
    {
        ConcurrentQueue<UInt256> PendingTransactionsHashes { get; }
        void AddTransactionHash(UInt256 hash);
    }
}