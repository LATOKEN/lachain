using System;
using System.Threading;
using System.Threading.Tasks;
using NeoSharp.Core.Helpers;
using NeoSharp.Core.Logging;
using NeoSharp.Core.Models;
using NeoSharp.Core.Models.OperationManager;

namespace NeoSharp.Core.Blockchain.Processing.BlockProcessing
{
    public class BlockProcessor : IBlockProcessor
    {
        private static readonly TimeSpan DefaultBlockPollingInterval = TimeSpan.FromMilliseconds(1_000);

        private readonly IBlockPool _blockPool;
        private readonly IAsyncDelayer _asyncDelayer;
        private readonly ISigner<Block> _blockSigner;
        private readonly IBlockPersister _blockPersister;
        private readonly IBlockchainContext _blockchainContext;
        private readonly ILogger<BlockProcessor> _logger;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private class CurrentBlockIndex
        {
            public int Index { get; set; } = -1;
        }

        private readonly CurrentBlockIndex _currentBlockIndex = new CurrentBlockIndex();
        
        public BlockProcessor(
            IBlockPool blockPool,
            IAsyncDelayer asyncDelayer,
            ISigner<Block> blockSigner,
            IBlockPersister blockPersister,
            IBlockchainContext blockchainContext,
            ILogger<BlockProcessor> logger)
        {
            _blockPool = blockPool ?? throw new ArgumentNullException(nameof(blockPool));
            _asyncDelayer = asyncDelayer ?? throw new ArgumentNullException(nameof(asyncDelayer));
            _blockSigner = blockSigner ?? throw new ArgumentNullException(nameof(blockSigner));
            _blockPersister = blockPersister ?? throw new ArgumentNullException(nameof(blockPersister));
            _blockchainContext = blockchainContext ?? throw new ArgumentNullException(nameof(blockchainContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        public void Run()
        {
            var cancellationToken = _cancellationTokenSource.Token;
            if (_blockchainContext.CurrentBlock != null)
                _currentBlockIndex.Index = (int) _blockchainContext.CurrentBlock.Index;

            Task.Factory.StartNew(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var nextBlockHeight = _blockchainContext.CurrentBlock?.Index + 1U ?? 0U;

                    if (!_blockPool.TryRemove(nextBlockHeight, out var block))
                    {
                        await _asyncDelayer.Delay(DefaultBlockPollingInterval, cancellationToken);
                        continue;
                    }
                    
                    await _blockPersister.Persist(block);
                    
                    lock (_currentBlockIndex)
                    {
                        if (_blockchainContext.CurrentBlock != null)
                            _currentBlockIndex.Index = (int) _blockchainContext.CurrentBlock.Index;
                        Monitor.PulseAll(_currentBlockIndex);
                    }
                }
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        /// <inheritdoc />
        public Task<Block> AddBlock(Block block)
        {
            if (block == null)
                throw new ArgumentNullException(nameof(block));

            var currentBlockHeight = _blockchainContext.CurrentBlock?.Index ?? -1U;
            if (currentBlockHeight >= block.Index || block.Index > currentBlockHeight + _blockPool.Capacity)
                return null;

            if (block.Hash == null)
                _blockSigner.Sign(block);

            var blockHash = block.Hash;
            if (blockHash == null || blockHash == UInt256.Zero)
                throw new ArgumentException(nameof(blockHash));

            if (_blockPool.TryAdd(block))
                _logger.LogWarning($"The block \"{blockHash.ToString(true)}\" was already queued to be added.");

            /* TODO: "why not to persist block here?" */
            return Task.FromResult(block);
        }
        
        public void WaitUntilBlockProcessed(uint index)
        {
            lock (_currentBlockIndex)
            {
                while (_currentBlockIndex.Index < index)
                    Monitor.Wait(_currentBlockIndex, TimeSpan.FromSeconds(1));
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
        }
    }
}