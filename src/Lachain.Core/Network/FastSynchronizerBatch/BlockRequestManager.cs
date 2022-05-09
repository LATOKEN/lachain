/*
    Here we are downloading block headers. This part is quite slow. Needs investigation.
*/
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
        public bool TryGetBatch(out List<ulong> batch)
        {
            batch = new List<ulong>();
            for(ulong i=0; i<_batchSize && nextBlocksToDownload.Count>0; i++)
            {
                ulong blockId = nextBlocksToDownload.Min;
                _pending.Add(blockId);
                batch.Add(blockId);
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
        public void HandleResponse(List<ulong> batch, JArray response)
        {
            if(batch.Count>0) Logger.LogInformation("First Node in this batch: " + batch[0]);
            if(batch.Count != response.Count)
            {
                foreach(var blockId in batch)
                {
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
                    ulong blockId = batch[i];
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
            while(downloaded.TryGetValue(_done+1, out var blockRawHex))
            {
                _nodeStorage.AddBlock(_blockSnapshot, blockRawHex);
                _done++;
                downloaded.Remove(_done);
            }
    /*        if(downloaded.Count>0) Console.WriteLine("More blocks downloaded. Done: " + 
            _done + " downloaded: "+downloaded.Count+" NextBlockToDownload " +
            nextBlocksToDownload.Count+ " pending: "+_pending.Count+" Min pending: "+ _pending.Min()
            +" Min to download: "+nextBlocksToDownload.Min());
*/
        }
    }
}
