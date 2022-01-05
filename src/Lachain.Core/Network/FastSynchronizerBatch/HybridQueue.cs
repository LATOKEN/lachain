using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Lachain.Storage;
using Lachain.Storage.Trie;
using Lachain.Utility.Utils;
using Lachain.Core.RPC.HTTP.Web3;
using Lachain.Utility.Serialization;
using Lachain.Logger;

namespace Lachain.Core.Network.FastSynchronizerBatch
{
    class HybridQueue{
        private static readonly ILogger<HybridQueue> Logger = LoggerFactory.GetLoggerForClass<HybridQueue>();
        private IRocksDbContext _dbContext;
        private const int BatchSize = 5000;
        private ulong _doneBatch=0, _totalBatch=0, _savedBatch=0;        
        private IDictionary<string, ulong> _pending = new Dictionary<string, ulong>();
        private Queue<string> _incomingQueue = new Queue<string>();
        private Queue<string> _outgoingQueue = new Queue<string>();
        private Queue<ulong> _batchQueue = new Queue<ulong>();


        private IDictionary<ulong, int> _remaining = new Dictionary<ulong, int>();

        private NodeStorage _nodeStorage;
        public HybridQueue(IRocksDbContext dbContext, NodeStorage nodeStorage)
        {
            _dbContext = dbContext;
            _nodeStorage = nodeStorage;
        }

        public void init()
        {
            _savedBatch = SerializationUtils.ToUInt64(_dbContext.Get(EntryPrefix.SavedBatch.BuildPrefix()));
            _doneBatch = _savedBatch;
            _totalBatch = SerializationUtils.ToUInt64(_dbContext.Get(EntryPrefix.TotalBatch.BuildPrefix()));
            
            Logger.LogInformation($"Starting with....");
            Logger.LogInformation($"Done Batch: {_doneBatch} Total Batch: {_totalBatch}");
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Add(string key)
        {
            if(_pending.ContainsKey(key)){
                _outgoingQueue.Enqueue(key);
                _batchQueue.Enqueue(_pending[key]);
                _pending.Remove(key);
                return;
            }
        //    bool foundHash = _nodeStorage.GetIdByHash(key, out ulong id);
        //    Console.WriteLine("Added to incomingqueue: "+id);
            _incomingQueue.Enqueue(key);
            if(_incomingQueue.Count >= BatchSize) PushToDB();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryGetValue(out string key)
        {
        //    Console.WriteLine("Outgoing queue: "+_outgoingQueue.Count+" Incoming Queue: "+_incomingQueue.Count);
            key = null;
            ulong batch;
            if(_outgoingQueue.Count>0)
            {
                key = _outgoingQueue.Dequeue();
                batch = _batchQueue.Dequeue();
            }
            else{
            //    if(_pending.Count!=0) return false;
                if(_doneBatch==_totalBatch && _incomingQueue.Count>0) PushToDB();
                while(_doneBatch<_totalBatch && _outgoingQueue.Count==0)
                {
                    LoadFromDB();
                }
                if(_outgoingQueue.Count==0) return false;
                key = _outgoingQueue.Dequeue();
                batch = _batchQueue.Dequeue();
            }
//            if(_pending.ContainsKey(key)) Console.WriteLine("something is not okay!----------------------------------: "+ _pending[key]+" "+_remaining[_pending[key]]);
            _pending[key] = batch;
            return true;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool ReceivedNode(string key)
        {
            ulong batch = _pending[key];
            _pending.Remove(key);
            _remaining[batch] = _remaining[batch] - 1;
            if(_remaining[batch]==0) Logger.LogInformation($"Node batch: {batch}  download done.");
            TryToSaveBatch();
            return true;
        }

        public void TryToSaveBatch()
        {
            while(_remaining.ContainsKey(_savedBatch+1) && _remaining[_savedBatch+1]==0)
            {
                _remaining.Remove(_savedBatch+1);
                PushToDB();
                _nodeStorage.Commit();
                _savedBatch++;
                _dbContext.Save(EntryPrefix.SavedBatch.BuildPrefix(), _savedBatch.ToBytes().ToArray());
                Logger.LogInformation($"Another batch saved: {_savedBatch}");
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Complete()
        {
            return _doneBatch==_totalBatch && _incomingQueue.Count==0 && _outgoingQueue.Count==0 && _pending.Count==0;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool isPending(string key)
        {
            return _pending.ContainsKey(key);
        }

        void PushToDB()
        {
            if(_incomingQueue.Count==0) return;
            _nodeStorage.CommitIds();
            List<byte> list = new List<byte>();
            int sz = _incomingQueue.Count;
            while(_incomingQueue.Count>0)
            {
                string hash = _incomingQueue.Dequeue();
            //    bool foundHash = _nodeStorage.GetIdByHash(hash, out ulong id);
            //    Console.WriteLine("adding id for download: "+ id);
                list.AddRange(HexUtils.HexToBytes(hash));
            }
            _totalBatch++;
            _dbContext.Save(EntryPrefix.QueueBatch.BuildPrefix((ulong)_totalBatch), list.ToArray());
            _dbContext.Save(EntryPrefix.TotalBatch.BuildPrefix(), _totalBatch.ToBytes().ToArray());
            Logger.LogInformation($"Another hash batch downloaded: {_totalBatch}  size: {sz}");
        }

        void LoadFromDB()
        {
            ulong _curBatch = _doneBatch+1;
            byte[] raw = _dbContext.Get(EntryPrefix.QueueBatch.BuildPrefix((ulong)_curBatch));
            int cnt = 0;
            for(int i=0; i<raw.Length; )
            {
                byte[] array = new byte[32];
                for(int j=0; j<32 ; j++,i++) array[j] = raw[i];
                string hash = HexUtils.ToHex(array);
                if(ExistNode(hash)) continue;
                _outgoingQueue.Enqueue(hash);
                _batchQueue.Enqueue(_curBatch);
                cnt++;
            }
            _remaining[_curBatch] = cnt;
            _doneBatch = _curBatch;
            Logger.LogInformation($"Trying to download nodes from batch: {_curBatch}  size: {cnt}");
            if(cnt==0) TryToSaveBatch();
        }
        bool ExistNode(string hash)
        {
            if(_pending.ContainsKey(hash)) return true;
            return _nodeStorage.ExistNode(hash);
        }
    }
}