using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Lachain.Core.Blockchain.Interface;
using Lachain.Logger;
using Lachain.Storage.State;
using Lachain.Utility.Utils;

namespace Lachain.Core.BlockchainFilter
{
    public class BlockchainEventFilter : IBlockchainEventFilter
    {
        private readonly IStateManager _stateManager;
        private readonly IBlockManager _blockManager;
        private static readonly ILogger<BlockchainEventFilter> Logger =
            LoggerFactory.GetLoggerForClass<BlockchainEventFilter>();

        private readonly Dictionary<ulong, BlockchainEventFilterParams> _filters =
            new Dictionary<ulong, BlockchainEventFilterParams>();

        private ulong _currentId;

        public BlockchainEventFilter(IBlockManager blockManager, IStateManager stateManager)
        {
            _blockManager = blockManager;
            _stateManager = stateManager;
            _currentId = 0;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public ulong Create(BlockchainEvent eventType)
        {
            _currentId++;
            _filters[_currentId] = new BlockchainEventFilterParams(eventType, _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight());
            return _currentId;
    }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Remove(ulong id)
        {
            if (_filters.TryGetValue(id, out _))
            {
                _filters.Remove(id);
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public string[] Sync(ulong id)
        {
            var results = new List<string>();
            if (_filters.TryGetValue(id, out var filter))
            {
                var lastSyncedBlock = filter.LastSyncedBlock;
                var highestBlock = _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight();
                for (var i = lastSyncedBlock; i < highestBlock; i++)
                {
                    var block = _blockManager.GetByHeight(i);
                    switch (filter.EventType)
                    {
                        case BlockchainEvent.Block:
                        {
                            results.Add(block!.Hash.ToHex());
                            break;
                        }
                        case BlockchainEvent.Transaction:
                        {
                            foreach (var txHash in block!.TransactionHashes)
                            {
                                var tx = _stateManager.LastApprovedSnapshot.Transactions.GetTransactionByHash(txHash);
                                results.Add(tx!.Hash.ToHex());
                            }
                            break;
                        }
                    }
                }

                filter.LastSyncedBlock = highestBlock;
                _filters[id] = filter;
            }
            return results.ToArray();
        }
    }
}