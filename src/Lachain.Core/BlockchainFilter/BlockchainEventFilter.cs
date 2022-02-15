using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Lachain.Logger;
using Lachain.Core.Blockchain.Interface;
using Lachain.Storage.State;
using Lachain.Utility.Utils;
using Lachain.Core.Blockchain.Pool;
using Newtonsoft.Json.Linq;
using Lachain.Core.RPC.HTTP.Web3;
using Lachain.Proto;

namespace Lachain.Core.BlockchainFilter
{
    public class BlockchainEventFilter : IBlockchainEventFilter
    {
        private readonly IStateManager _stateManager;
        private readonly IBlockManager _blockManager;
        private readonly ITransactionPool _transactionPool;
        private static readonly ILogger<BlockchainEventFilter> Logger =
            LoggerFactory.GetLoggerForClass<BlockchainEventFilter>();

        private readonly Dictionary<ulong, BlockchainEventFilterParams> _filters =
            new Dictionary<ulong, BlockchainEventFilterParams>();

        private ulong _currentId;

        public BlockchainEventFilter(IBlockManager blockManager, IStateManager stateManager, ITransactionPool transactionPool)
        {
            _blockManager = blockManager;
            _stateManager = stateManager;
            _transactionPool = transactionPool;
            _currentId = 0;
        }


        [MethodImpl(MethodImplOptions.Synchronized)]
        public ulong Create(BlockchainEvent eventType)
        {
            RemoveUnusedFilters();
            _currentId++;
            if(eventType == BlockchainEvent.Block)
                _filters[_currentId] = new BlockchainEventFilterParams(
                    eventType, _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight()
                );
            else if(eventType == BlockchainEvent.Transaction)
                _filters[_currentId] = new BlockchainEventFilterParams(
                    eventType, _transactionPool.Transactions.Keys.ToArray()
                );
            else{
                Logger.LogError($"Implementation not found for filter type: {eventType}");
            }
            return _currentId;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public ulong Create(BlockchainEvent eventType, ulong? fromBlock, ulong? toBlock,  List<UInt160> addresses, List<List<UInt256>> topics)
        {
            RemoveUnusedFilters();
            _currentId++;
            if(eventType == BlockchainEvent.Logs){
                _filters[_currentId] = new BlockchainEventFilterParams(
                    eventType, fromBlock, toBlock, addresses, topics
                );
            }
            else{
                Logger.LogError($"Implementation not found for filter type: {eventType}");
            }
            return _currentId;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Remove(ulong id)
        {
            if (_filters.TryGetValue(id, out _))
            {
                _filters.Remove(id);
                return true;
            }
            else{
                Logger.LogTrace($"Filter: {id} not found, possibly removed after timeout.");
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void RemoveUnusedFilters(){
            var currentTime = TimeUtils.CurrentTimeMillis();
            foreach(var (filterId , filterParams) in _filters){
                if(currentTime - filterParams.PollingTime >= TimeOut()) Remove(filterId);
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public ulong TimeOut(){
            return 600000; // in ms, set to 10 mins
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public JArray SyncBlocks(ulong id, BlockchainEventFilterParams filter, bool poll)
        {
            
            var results = new JArray();
            var lastSyncedBlock = filter.LastSyncedBlock;
            var highestBlock = _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight();
            for (var i = lastSyncedBlock; i < highestBlock; i++)
            {
                var block = _blockManager.GetByHeight(i);
                if(block is null) continue;
                results.Add(Web3DataFormatUtils.Web3Data(block!.Hash));
            }

            if(poll){
                filter.LastSyncedBlock = highestBlock;
                filter.PollingTime = TimeUtils.CurrentTimeMillis();
                _filters[id] = filter;
            }
            return results;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public JArray SyncPendingTransaction(ulong id, BlockchainEventFilterParams filter, bool poll)
        {
            
            var results = new JArray();
            
            var pendingTx = filter.PendingTransactionList;
            var poolTx = _transactionPool.Transactions.Keys.ToArray();
            Array.Sort(poolTx, (x,y) => UInt256Utils.Compare(x,y));

            // comparing two sorted list listA and listB in O(listA.Count + listB.Count)
            
            int iter = 0;
            foreach(var txHash in poolTx){
                if(txHash is null) continue;

                while(iter < pendingTx.Count && UInt256Utils.Compare(txHash,pendingTx[iter]) > 0) iter++;
                if(iter == pendingTx.Count || UInt256Utils.Compare(txHash,pendingTx[iter]) < 0){
                    results.Add(Web3DataFormatUtils.Web3Data(txHash));
                }
                
            }

            if(poll){
                // Pending transaction list in filter must be sorted for further optimization
                filter.PendingTransactionList = new List<UInt256>(poolTx.ToList());
                filter.PollingTime = TimeUtils.CurrentTimeMillis();
                _filters[id] = filter;
            }
            return results;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public JArray GetLogs(ulong id, BlockchainEventFilterParams filter, bool poll)
        {
            if(poll){
                filter.PollingTime = TimeUtils.CurrentTimeMillis();
                _filters[id] = filter;
            }
            if((filter.FromBlock is null) || (filter.ToBlock is null)) return new JArray();
            return GetLogs((ulong)filter.FromBlock, (ulong)filter.ToBlock, filter.AddressList, filter.TopicLists);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public JArray GetLogs(ulong start, ulong finish, List<UInt160> addresses, List<List<UInt256>> allTopics)
        {
            var jArray = new JArray();
            for (var blockNumber = start; blockNumber <= finish; blockNumber++)
            {
                var block = _blockManager.GetByHeight(blockNumber);
                if (block == null)
                    continue;
                var txs = block!.TransactionHashes;
                foreach (var tx in txs)
                {
                    

                    var txEvents = _stateManager.LastApprovedSnapshot.Events.GetTotalTransactionEvents(tx);
                    for (var i = 0; i < txEvents; i++)
                    {
                        var txEventObj = _stateManager.LastApprovedSnapshot.Events.GetEventByTransactionHashAndIndex(tx,
                            (uint)i);
                        var txEvent = txEventObj._event;
                        if (txEvent is null)
                            continue;
                        
                        if(!addresses.Any(a => txEvent.Contract.Equals(a))) continue;

                        var txTopics = new List<UInt256>();
                        txTopics.Add(txEvent.SignatureHash);
                        if(txEventObj._topics != null){
                            foreach(var topic in txEventObj._topics) 
                            {
                                txTopics.Add(topic);
                            }
                        }

                        if (!MatchTopics(allTopics, txTopics))
                        {
                            Logger.LogInformation($"Skip event with signature [{txEvent.SignatureHash.ToHex()}]");
                            continue;
                        }

                        if (txEvent.BlockHash is null || txEvent.BlockHash.IsZero())
                        {
                            txEvent.BlockHash = block.Hash;
                        }
           
                        jArray.Add(Web3DataFormatUtils.Web3Event(txEventObj, blockNumber,block.Hash));
                    }
                }
            }

            return jArray;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public JArray Sync(ulong id, bool poll)
        {
            RemoveUnusedFilters();
            var results = new JArray();
            if (_filters.TryGetValue(id, out var filter))
            {
                if(filter.EventType == BlockchainEvent.Block){
                    results = SyncBlocks(id, filter, poll);
                }
                else if(filter.EventType == BlockchainEvent.Transaction){
                    results = SyncPendingTransaction(id , filter, poll);
                }
                else if(filter.EventType == BlockchainEvent.Logs){
                    results = GetLogs(id,  filter, poll);
                }
                else{
                    Logger.LogError($"Implementation not found for filter type: {filter.EventType}");
                }
            }
            else{
                Logger.LogTrace($"Filter: {id} not found, possibly removed after timeout.");
            }
            return results;
        }

        public bool MatchTopics(List<List<UInt256>> allTopics, List<UInt256> txTopics)
        {
            for(int i = 0 ; i < 4 ; i++){
                if(allTopics[i].Count > 0){
                    if(txTopics.Count <= i) return false;
                    if(!allTopics[i].Any(t => txTopics[i].Equals(t))) return false;
                }
            }
            return true;
        }
    }
}