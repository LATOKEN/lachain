using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Lachain.Storage.Trie;
using Lachain.Utility.Utils;
using Lachain.Core.RPC.HTTP.Web3;
using Lachain.Storage.State;
using Lachain.Logger;


namespace Lachain.Core.Network.FastSynchronizerBatch
{
    class BlockRequestManager
    {
        private static readonly ILogger<BlockRequestManager> Logger = LoggerFactory.GetLoggerForClass<BlockRequestManager>();
 //       private Queue<string> _queue = new Queue<string>();
        private SortedSet<ulong> _pending = new SortedSet<ulong>();
        private SortedSet<ulong> nextBlocksToDownload = new SortedSet<ulong>();
        private IDictionary<ulong, string> downloaded = new Dictionary<ulong,string>();
        private uint _batchSize = 1000;
        ulong _done = 0;
        ulong _maxBlock;

        IBlockSnapshot _blockSnapshot;
        NodeStorage _nodeStorage; 

        public BlockRequestManager(IBlockSnapshot blockSnapshot, ulong maxBlock, NodeStorage nodeStorage)
        {
            _nodeStorage = nodeStorage;
            _blockSnapshot = blockSnapshot;
            _done = blockSnapshot.GetTotalBlockHeight();
            _maxBlock = maxBlock;
            for(ulong i=_done+1; i<=_maxBlock; i++) nextBlocksToDownload.Add(i);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryGetBatch(out List<string> batch)
        {
            batch = new List<string>();
            for(ulong i=0; i<_batchSize && nextBlocksToDownload.Count>0; i++)
            {
                ulong blockId = nextBlocksToDownload.Min;
                _pending.Add(blockId);
                batch.Add(HexUtils.ToHex(blockId));
                nextBlocksToDownload.Remove(blockId);
            } 
            if (batch.Count == 0) return false;

            return true;
        }
        
        public bool Done()
        {
            return _done==_maxBlock && _pending.Count==0;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void HandleResponse(List<string> batch, JArray response)
        {
            if(batch.Count>0) Logger.LogInformation("First Node in this batch: "+Convert.ToUInt64(batch[0], 16));

            if(batch.Count != response.Count)
            {
                foreach(var block in batch)
                {
                    ulong blockId = Convert.ToUInt64(block, 16);
                    if(_pending.Contains(blockId))
                    {
                        _pending.Remove(blockId);
                        nextBlocksToDownload.Add(blockId);
                    }
                }
            }
            else{
                for(int i=0; i<batch.Count; i++)
                {
                    ulong blockId = Convert.ToUInt64(batch[i], 16);
                    if(_pending.Contains(blockId))
                    {
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
            ulong cnt = _done ;
            DateTime start = DateTime.Now;
            while(downloaded.TryGetValue(_done+1, out var blockRawHex))
            {
                _nodeStorage.AddBlock(_blockSnapshot, blockRawHex);
                _done++;
                
                downloaded.Remove(_done);
            }
        }
    }
}
