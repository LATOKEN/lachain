using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NeoSharp.Core.Blockchain;
using NeoSharp.Core.Helpers;
using NeoSharp.Core.Messaging.Messages;

namespace NeoSharp.Core.Network
{
    class TransactionCrawler : ITransactionCrawler, IDisposable
    {
        public ConcurrentQueue<UInt256> PendingTransactionsHashes { get; } = new ConcurrentQueue<UInt256>();

        private CancellationTokenSource _tokenSource;
        private readonly IAsyncDelayer _asyncDelayer;
        private readonly IServerContext _serverContext;
        
        private static readonly TimeSpan DefaultProcessStartingDelay = TimeSpan.FromMilliseconds(10_000);
        private static readonly TimeSpan DefaultTransactionWaitingInterval = TimeSpan.FromMilliseconds(100);
        private static readonly int TransactionsPerBatch = 500;

        public TransactionCrawler(
            IAsyncDelayer asyncDelayer, IServerContext serverContext, ITransactionPool transactionPool
        )
        {
            _asyncDelayer = asyncDelayer ?? throw new ArgumentNullException(nameof(asyncDelayer));
            _serverContext = serverContext ?? throw new ArgumentNullException(nameof(serverContext));
        }

        public void Start()
        {
            Stop();

            _tokenSource = new CancellationTokenSource();
            var cancellationToken = _tokenSource.Token;

            Task.Factory.StartNew(async () =>
            {
                await _asyncDelayer.Delay(DefaultProcessStartingDelay, cancellationToken);

                while (!cancellationToken.IsCancellationRequested)
                {
                    if (PendingTransactionsHashes.Count == 0)
                    {
                        await _asyncDelayer.Delay(DefaultTransactionWaitingInterval, cancellationToken);
                        continue;
                    }

                    List<UInt256> hashes = new List<UInt256>();
                    while (hashes.Count < TransactionsPerBatch && !PendingTransactionsHashes.IsEmpty)
                    {
                        if (!PendingTransactionsHashes.TryDequeue(out var hash)) break;
                        hashes.Add(hash);
                    }

                    if (!hashes.Any()) continue;
                    try
                    {
                        var connectedPeers = _serverContext.ConnectedPeers.Values
                            .Where(p => p.IsConnected)
                            .ToArray();
                        Parallel.ForEach(
                            connectedPeers,
                            async peer => await peer.Send(new GetDataMessage(InventoryType.Transaction, hashes))
                        );
                    }
                    catch
                    {
                        // enqueue again to retry eventually
                        foreach (var hash in hashes)
                        {
                            PendingTransactionsHashes.Enqueue(hash);
                        }
                    }
                }
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public void Stop()
        {
            if (_tokenSource == null) return;

            _tokenSource.Cancel();
            _tokenSource.Dispose();
            _tokenSource = null;
        }

        public void AddTransactionHash(UInt256 hash)
        {
            PendingTransactionsHashes.Enqueue(hash);
        }

        public void Dispose()
        {
            _tokenSource?.Cancel();
            _tokenSource?.Dispose();
            _tokenSource = null;
        }
    }
}