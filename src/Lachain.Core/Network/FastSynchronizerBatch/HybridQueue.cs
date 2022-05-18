/*
    We are downloading the tree in a breadth-first-search style, so the required queue might become very large once we
    go a little bit deep in the tree. So we need to store the queue in disk. But again writing/reading in disk for every single node will be
    very time consuming. We need to do it in batch too. HybridQueue combines the disk and memory to reduce latency.
*/

using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Storage;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;


namespace Lachain.Core.Network.FastSynchronizerBatch
{
    public class HybridQueue : IHybridQueue
    {
        private static readonly ILogger<HybridQueue> Logger = LoggerFactory.GetLoggerForClass<HybridQueue>();
        private IRocksDbContext _dbContext;
        // maximum how many nodes we should put into/out of db at once 
        private const int BatchSize = 5000;

        // _totalBatch means how many batch of nodeHashes in total we know about(including the nodeHashes that for whom corresponding node is downloaded)
        // _savedBatch means for how many batch of nodeHashes we have received node data
        // _loadedBatch means how many batch of nodeHashes have been loaded from database into _outgoingQueue(for requesting to peers) 
        private ulong _loadedBatch=0, _totalBatch=0, _savedBatch=0;
        // nodes which are requested but has not arrived yet resides in the _pending container, mapping is nodeHash -> node's batch        
        private IDictionary<UInt256, ulong> _pending = new Dictionary<UInt256, ulong>();
        // every node at first enters to _incomingQueue before getting sent to database
        private Queue<UInt256> _incomingQueue = new Queue<UInt256>();
        // when we take out nodes from database we keep it in _outgoingQueue and it's batch in the _batchQueue
        private Queue<UInt256> _outgoingQueue = new Queue<UInt256>();
        private Queue<ulong> _batchQueue = new Queue<ulong>();

        // we need to keep track of how many nodes for a batch has not arrived till now
        private IDictionary<ulong, int> _remaining = new Dictionary<ulong, int>();

        private IFastSyncRepository _repository;
        public HybridQueue(IRocksDbContext dbContext, IFastSyncRepository repository)
        {
            _dbContext = dbContext;
            _repository = repository;
        }

        public void Initialize()
        {
            _savedBatch = SerializationUtils.ToUInt64(_dbContext.Get(EntryPrefix.SavedBatch.BuildPrefix()));
            _loadedBatch = _savedBatch;
            _totalBatch = SerializationUtils.ToUInt64(_dbContext.Get(EntryPrefix.TotalBatch.BuildPrefix()));
            
            Logger.LogTrace($"Starting with....");
            Logger.LogTrace($"Done Batch: {_loadedBatch} Total Batch: {_totalBatch}");
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Add(UInt256 key)
        {
            if(_pending.ContainsKey(key)){
                _outgoingQueue.Enqueue(key);
                _batchQueue.Enqueue(_pending[key]);
                _pending.Remove(key);
                return;
            }
            _incomingQueue.Enqueue(key);
            if(_incomingQueue.Count >= BatchSize) PushToDB();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryGetValue(out UInt256? key)
        {
            key = null;
            ulong batch;
            if(_outgoingQueue.Count>0)
            {
                key = _outgoingQueue.Dequeue();
                batch = _batchQueue.Dequeue();
            }
            else{
                // All the nodeHashes must go through _incomingQueue -> Database -> _outgoingQueue
                if(_loadedBatch==_totalBatch && _incomingQueue.Count>0) PushToDB();
                while(_loadedBatch<_totalBatch && _outgoingQueue.Count==0)
                {
                    LoadFromDB();
                }
                if(_outgoingQueue.Count==0) return false;
                key = _outgoingQueue.Dequeue();
                batch = _batchQueue.Dequeue();
            }
            _pending[key] = batch;
            return true;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool ReceivedNode(UInt256 key)
        {
            ulong batch = _pending[key];
            _pending.Remove(key);
            _remaining[batch] = _remaining[batch] - 1;
            if(_remaining[batch]==0) Logger.LogInformation($"Node batch: {batch}  download done.");
            TryToSaveBatch();
            return true;
        }

        private void TryToSaveBatch()
        {
            // batches are saved one by one, batch x+1 will not be saved until batch x is saved
            while(_remaining.ContainsKey(_savedBatch+1) && _remaining[_savedBatch+1]==0)
            {
                _remaining.Remove(_savedBatch+1);
                PushToDB();
                _repository.Commit();
                _savedBatch++;
                _dbContext.Save(EntryPrefix.SavedBatch.BuildPrefix(), _savedBatch.ToBytes().ToArray());
                Logger.LogInformation($"Another batch saved: {_savedBatch}");
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Complete()
        {
            return _loadedBatch==_totalBatch && _incomingQueue.Count==0 && _outgoingQueue.Count==0 && _pending.Count==0;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool isPending(UInt256 key)
        {
            return _pending.ContainsKey(key);
        }

        private void PushToDB()
        {
            if(_incomingQueue.Count==0) return;
            _repository.CommitIds();
            List<byte> list = new List<byte>();
            int sz = _incomingQueue.Count;
            while(_incomingQueue.Count>0)
            {
                var hash = _incomingQueue.Dequeue();
                list.AddRange(hash.ToBytes());
            }
            _totalBatch++;
            _dbContext.Save(EntryPrefix.QueueBatch.BuildPrefix((ulong)_totalBatch), list.ToArray());
            _dbContext.Save(EntryPrefix.TotalBatch.BuildPrefix(), _totalBatch.ToBytes().ToArray());
            Logger.LogInformation($"Another hash batch downloaded: {_totalBatch}  size: {sz}");
        }

        private void LoadFromDB()
        {
            ulong _curBatch = _loadedBatch+1;
            byte[] raw = _dbContext.Get(EntryPrefix.QueueBatch.BuildPrefix((ulong)_curBatch));
            int cnt = 0;
            for(int i=0; i<raw.Length; )
            {
                byte[] array = new byte[32];
                for(int j=0; j<32 ; j++,i++) array[j] = raw[i];
                var hash = UInt256Utils.ToUInt256(array);
                if(ExistNode(hash)) continue;
                _outgoingQueue.Enqueue(hash);
                _batchQueue.Enqueue(_curBatch);
                cnt++;
            }
            _remaining[_curBatch] = cnt;
            _loadedBatch = _curBatch;
            Logger.LogInformation($"Trying to download nodes from batch: {_curBatch}  size: {cnt}");
            if(cnt==0) TryToSaveBatch();
        }
        private bool ExistNode(UInt256 hash)
        {
            if(_pending.ContainsKey(hash)) return true;
            return _repository.ExistNode(hash);
        }
    }
}