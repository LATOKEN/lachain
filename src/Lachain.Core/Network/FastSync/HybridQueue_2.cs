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

namespace Lachain.Core.Network.FastSynchronizerBatch
{
    class HybridQueue2{
        private IRocksDbContext _dbContext;
        private const int BatchSize = 5000;
        private ulong _doneBatch=0, _totalBatch=0;        
        private HashSet<string> _pending = new HashSet<string>();
        private Queue<string> _incomingQueue = new Queue<string>();
        private Queue<string> _outgoingQueue = new Queue<string>();

        private IDictionary<string, ulong> _exists = new Dictionary<string, ulong>();

        private NodeStorage _nodeStorage;
        public HybridQueue2(IRocksDbContext dbContext, NodeStorage nodeStorage)
        {
            _dbContext = dbContext;
            _nodeStorage = nodeStorage;
        }

        public void init()
        {
            _doneBatch = SerializationUtils.ToUInt64(_dbContext.Get(EntryPrefix.DoneBatch.BuildPrefix()));
            _totalBatch = SerializationUtils.ToUInt64(_dbContext.Get(EntryPrefix.TotalBatch.BuildPrefix()));
            Console.WriteLine("Starting with....");
            Console.WriteLine("Done Batch: "+_doneBatch+" Total Batch: "+_totalBatch);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Add(string key)
        {
            if(_pending.Contains(key)){
                _pending.Remove(key);
                _outgoingQueue.Enqueue(key);
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
            if(_outgoingQueue.Count>0) key = _outgoingQueue.Dequeue();
            else{
                if(_pending.Count!=0) return false;
                if(_doneBatch==_totalBatch && _incomingQueue.Count>0) PushToDB();
                if(_doneBatch<_totalBatch){
                    LoadFromDB();
                    key = _outgoingQueue.Dequeue();
                }
                else return false;
            }
            if(_pending.Contains(key)) Console.WriteLine("something is not okay!--------------------------------------------");
            _pending.Add(key);
            return true;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool ReceivedNode(string key)
        {
            _pending.Remove(key);
            if(_pending.Count==0 &&_outgoingQueue.Count==0)
            {
                PushToDB();
                _nodeStorage.Commit();
                _doneBatch++;
                _dbContext.Save(EntryPrefix.DoneBatch.BuildPrefix(), _doneBatch.ToBytes().ToArray());
                Console.WriteLine("Another batch done: "+ _doneBatch);
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Complete()
        {
            return _doneBatch==_totalBatch && _incomingQueue.Count==0 && _outgoingQueue.Count==0 && _pending.Count==0;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool isPending(string key)
        {
            return _pending.Contains(key);
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
            Console.WriteLine("Another batch Downloaded: "+ _totalBatch+"  size: "+sz);
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
                String hash = HexUtils.ToHex(array);
                _outgoingQueue.Enqueue(hash);
                if( _exists.ContainsKey(hash)) Console.WriteLine("same key being loaded multiple times....................");
                else _exists[hash] = _curBatch;

                cnt++;
            }
            Console.WriteLine("Trying to download nodes from batch: "+_curBatch+"  size: "+ cnt);
        }
    }
}