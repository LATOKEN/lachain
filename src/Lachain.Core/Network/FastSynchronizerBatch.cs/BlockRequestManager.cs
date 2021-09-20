using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Lachain.Storage.Trie;
using Lachain.Utility.Utils;
using Lachain.Core.RPC.HTTP.Web3;
using Lachain.Storage.State;

namespace Lachain.Core.Network.FastSynchronizerBatch
{
    class BlockRequestManager
    {
 //       private Queue<string> _queue = new Queue<string>();
        private HashSet<ulong> _pending = new HashSet<ulong>();
        private SortedSet<ulong> nextBlocksToDownload = new SortedSet<ulong>();
        private IDictionary<ulong, string> downloaded = new Dictionary<ulong,string>();
        private uint _batchSize = 40;
        ulong _done = 0;
        ulong _maxBlock;

        IBlockSnapshot _blockSnapshot;
        NodeStorage _nodeStorage; 

        public BlockRequestManager(IBlockSnapshot blockSnapshot, ulong maxBlock, NodeStorage nodeStorage)
        {
            _nodeStorage = nodeStorage;
            _blockSnapshot = blockSnapshot;
            _done = 0;
            _maxBlock = maxBlock;
            for(ulong i=1; i<=_maxBlock; i++) nextBlocksToDownload.Add(i);
        }

        public bool TryGetBatch(out List<string> batch)
        {
            batch = new List<string>();
            lock(this)
            {
                for(ulong i=0; i<_batchSize && nextBlocksToDownload.Count>0; i++)
                {
                    ulong blockId = nextBlocksToDownload.Min();
                    _pending.Add(blockId);
                    batch.Add(HexUtils.ToHex(blockId));
                    nextBlocksToDownload.Remove(blockId);
                } 
            }
            if (batch.Count == 0) return false;

            return true;
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Done()
        {
            return _done==_maxBlock && _pending.Count==0;
        }


        public void HandleResponse(List<string> batch, JArray response)
        {
            if(batch.Count != response.Count)
            {
                lock(this)
                {
                    foreach(var block in batch)
                    {
                        ulong blockId = Convert.ToUInt64(block,16);
                        _pending.Remove(blockId);
                        nextBlocksToDownload.Add(blockId);
                    }
                }
            }
            else{
                lock(this)
                {
                    for(int i=0; i<batch.Count; i++)
                    {
                        ulong blockId = Convert.ToUInt64(batch[i],16);
                        _pending.Remove(blockId);
                        downloaded[blockId] = (string)response[i]; 
                    }
                }
            }
            AddToDB();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        void AddToDB()
        {
            while(downloaded.TryGetValue(_done+1,out var blockRawHex))
            {
                _nodeStorage.AddBlock(_blockSnapshot, blockRawHex);
                _done++;
            }
        }
    }
}
