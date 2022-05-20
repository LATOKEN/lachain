/*
    Here we are downloading block headers. This part is quite slow. Needs investigation.
*/
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Lachain.Logger;
using Lachain.Proto;


namespace Lachain.Core.Network.FastSynchronizerBatch
{
    public class BlockRequestManager : IBlockRequestManager
    {
        private static readonly ILogger<BlockRequestManager> Logger = LoggerFactory.GetLoggerForClass<BlockRequestManager>();
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
        public void Initialize()
        {
            _done = _repository.GetCurrentBlockHeight();
            if (_maxBlock != 0)
                throw new Exception("Trying to initialize second time.");
            _maxBlock = _repository.GetCheckpointBlockNumber();
            if (_maxBlock < _done) throw new ArgumentOutOfRangeException("Max block is less then current block height");
            for (ulong i = _done+1; i <= _maxBlock; i++) nextBlocksToDownload.Add(i);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryGetBatch(out ulong fromBlock, out ulong toBlock)
        {
            fromBlock = _done + 1;
            toBlock = _done;
            if (nextBlocksToDownload.Count == 0) return false;
            fromBlock = toBlock = nextBlocksToDownload.Min;
            nextBlocksToDownload.Remove(fromBlock);
            for(var i = 1; i < _batchSize && nextBlocksToDownload.Count > 0; i++)
            {
                ulong blockId = nextBlocksToDownload.Min;
                if (blockId != toBlock + 1) break;
                toBlock = blockId;
                nextBlocksToDownload.Remove(blockId);
            } 
            return true;
        }
        
        public bool Done()
        {
            return _done == _maxBlock;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void HandleResponse(ulong fromBlock, ulong toBlock, List<Block> response)
        {
            try
            {
                if(fromBlock <= toBlock) Logger.LogInformation("First Node in this batch: " + fromBlock);
                if(ExpectedBlockCount(fromBlock, toBlock) != response.Count) throw new Exception("Invalid response");
                for(int i = 0; i < response.Count; i++)
                {
                    ulong blockId = (ulong) i + fromBlock;
                    var block = response[i];
                    if (block is null || blockId != block.Header.Index)
                        throw new Exception("Invalid response");
                }
                foreach (var block in response)
                {
                    downloaded[block.Header.Index] = block;
                }
                AddToDB();
            }
            catch (Exception)
            {
                for (var blockId = fromBlock; blockId <= toBlock; blockId++)
                {
                    nextBlocksToDownload.Add(blockId);
                }
            }
        }

        public static int ExpectedBlockCount(ulong fromBlock, ulong toBlock)
        {
            return (int) (toBlock - fromBlock + 1);
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
