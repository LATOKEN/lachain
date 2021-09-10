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

namespace Lachain.Core.Network.FastSynchronizerBatch
{
    class HybridQueue{
        private IRocksDbContext _dbContext;
        private const int BatchSize = 1000;
        private int CurBatch, TotalBatch;
        private Queue<string> _queue = new Queue<string>(); 

        public int Count = 0;
        public HybridQueue(IRocksDbContext dbContext)
        {
            _dbContext = dbContext;
            CurBatch = 0;
            TotalBatch = 0; 
        }

        public void Enqueue(string val)
        {
            _queue.Enqueue(val);
            if(_queue.Count >= 2*BatchSize)
            {
                List<byte> list = new List<byte>();
                for(int i=0 ; i<BatchSize; i++)
                {
                //    byte[] array = HexUtils.HexToBytes(_queue.Dequeue());
                //    for(int j=0; j<array.Length; j++) list.Add(array[j]);
                    list.AddRange(HexUtils.HexToBytes(_queue.Dequeue()));
                }
                TotalBatch++;
                _dbContext.Save(EntryPrefix.QueueBatch.BuildPrefix((ulong)TotalBatch), list.ToArray());
            }
            Count++;
        }

        public string Dequeue()
        {
            Count--;
            if(_queue.Count > 0) return _queue.Dequeue();
            else if(CurBatch<TotalBatch){
                CurBatch++;
                byte[] raw = _dbContext.Get(EntryPrefix.QueueBatch.BuildPrefix((ulong)CurBatch));
                for(int i=0; i<raw.Length; )
                {
                    byte[] array = new byte[32];
                    for(int j=0; j<32 ; j++,i++) array[j] = raw[i];
                    _queue.Enqueue(HexUtils.ToHex(array));
                }
                return _queue.Dequeue();
            }
            else{
                return "0x";
            }
        }
    }
}