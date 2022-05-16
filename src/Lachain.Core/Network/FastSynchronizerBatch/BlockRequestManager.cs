/*
    Here we are downloading block headers. This part is quite slow. Needs investigation.
*/
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Lachain.Core.RPC.HTTP.Web3;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Storage.Trie;
using Lachain.Utility.Utils;


namespace Lachain.Core.Network.FastSynchronizerBatch
{
    public class BlockRequestManager : IBlockRequestManager
    {
        private static readonly ILogger<BlockRequestManager> Logger = LoggerFactory.GetLoggerForClass<BlockRequestManager>();
 //       private Queue<string> _queue = new Queue<string>();
        private SortedSet<ulong> _pending = new SortedSet<ulong>();
        private SortedSet<ulong> nextBlocksToDownload = new SortedSet<ulong>();
        private IDictionary<ulong, Block> downloaded = new Dictionary<ulong,Block>();
        private uint _batchSize = 1000;
        private ulong _done = 0;
        private ulong _maxBlock = 0;
        private readonly IFastSyncRepository _repository; 
        public ulong MaxBlock => _maxBlock;

        public BlockRequestManager(IFastSyncRepository repository)
        {
            _repository = repository;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void SetMaxBlock(ulong maxBlock)
        {
            _done = _repository.GetBlockHeight();
            if (_maxBlock != 0)
                throw new Exception("Trying to set max block second time.");
            _maxBlock = maxBlock;
            if (_maxBlock < _done) throw new ArgumentOutOfRangeException("Max block is less then current block height");
            for (ulong i = _done+1; i <= _maxBlock; i++) nextBlocksToDownload.Add(i);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryGetBatch(out List<ulong> batch)
        {
            batch = new List<ulong>();
            for(ulong i=0; i < _batchSize && nextBlocksToDownload.Count > 0; i++)
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
            return _done == _maxBlock && _pending.Count==0;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void HandleResponse(List<ulong> batch, List<Block> response)
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
                for(int i=0; i < batch.Count; i++)
                {
                    ulong blockId = batch[i];
                    if(_pending.Contains(blockId))
                    {
                        _pending.Remove(blockId);
                        downloaded[blockId] = response[i];
                    }
                }
            }
            AddToDB();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void AddToDB()
        {
            while(downloaded.TryGetValue(_done+1, out var block))
            {
                _repository.AddBlock(block);
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
