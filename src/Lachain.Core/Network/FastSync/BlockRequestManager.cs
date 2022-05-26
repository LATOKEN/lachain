/*
    Here we are downloading block headers. This part is quite slow. Needs investigation.
*/
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Interface;
using Lachain.Crypto;
using Lachain.Crypto.Misc;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Utility.Utils;


namespace Lachain.Core.Network.FastSync
{
    public class BlockRequestManager : IBlockRequestManager
    {
        private static readonly ILogger<BlockRequestManager> Logger = LoggerFactory.GetLoggerForClass<BlockRequestManager>();
        private SortedSet<ulong> _nextBlocksToDownload = new SortedSet<ulong>();
        private IDictionary<ulong, Block> _downloaded = new Dictionary<ulong,Block>();
        private IDictionary<ulong, (Block, ECDSAPublicKey?)> _blocksToVerify = new Dictionary<ulong, (Block, ECDSAPublicKey?)>();
        private uint _batchSize = 1000;
        private ulong _done = 0;
        private ulong _maxBlock = 0;
        private ulong _lastVerified = 0;
        private bool _verificationRunning = false;
        private readonly IFastSyncRepository _repository;
        private readonly IBlockManager _blockManager;
        private readonly Thread _blockVerifyThread;
        public ulong MaxBlock => _maxBlock;

        public BlockRequestManager(IFastSyncRepository repository, IBlockManager blockManager)
        {
            _repository = repository;
            _blockManager = blockManager;
            _blockVerifyThread = new Thread(RunBlockVerifier);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Initialize()
        {
            _done = _repository.GetCurrentBlockHeight();
            _lastVerified = _done;
            if (_maxBlock != 0)
                throw new Exception("Trying to initialize second time.");
            _maxBlock = _repository.GetCheckpointBlockNumber();
            if (_maxBlock < _done) throw new ArgumentOutOfRangeException("Max block is less then current block height");
            for (ulong i = _done+1; i <= _maxBlock; i++) _nextBlocksToDownload.Add(i);
            StartVerification();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryGetBatch(out ulong fromBlock, out ulong toBlock)
        {
            fromBlock = _done + 1;
            toBlock = _done;
            if (_nextBlocksToDownload.Count == 0) return false;
            fromBlock = toBlock = _nextBlocksToDownload.Min;
            _nextBlocksToDownload.Remove(fromBlock);
            for(var i = 1; i < _batchSize && _nextBlocksToDownload.Count > 0; i++)
            {
                ulong blockId = _nextBlocksToDownload.Min;
                if (blockId != toBlock + 1) break;
                toBlock = blockId;
                _nextBlocksToDownload.Remove(blockId);
            } 
            return true;
        }
        
        public bool Done()
        {
            return _done == _maxBlock;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void HandleResponse(ulong fromBlock, ulong toBlock, List<Block> response, ECDSAPublicKey? peer)
        {
            try
            {
                string peerPubkey = peer is null ? "null" : peer.ToHex();
                if(fromBlock <= toBlock) Logger.LogInformation("First Node in this batch: " + fromBlock);
                if(ExpectedBlockCount(fromBlock, toBlock) != response.Count) throw new Exception("Invalid response");
                for(int i = 0; i < response.Count; i++)
                {
                    ulong blockId = (ulong) i + fromBlock;
                    var block = response[i];
                    if (block is null)
                    {
                        Logger.LogWarning($"Got null block from peer {peerPubkey}");
                        throw new Exception($"Invalid response from peer {peerPubkey}: null block");
                    }
                    if (blockId != block.Header.Index)
                    {
                        Logger.LogWarning($"Got invalid block index from peer {peerPubkey}");
                        throw new Exception($"Invalid response from peer {peerPubkey}: index mismatch");
                    }
                }
                lock (_blocksToVerify)
                {
                    foreach (var block in response)
                    {
                        if (block.Header.Index > _lastVerified)
                            _blocksToVerify.TryAdd(block.Header.Index, (block, peer));
                    }
                }
            }
            catch (Exception)
            {
                for (var blockId = fromBlock; blockId <= toBlock; blockId++)
                {
                    _nextBlocksToDownload.Add(blockId);
                }
            }
        }

        public OperatingError VerifyBlock(Block? block)
        {
            if (block is null) return OperatingError.InvalidBlock;
            var header = block.Header;
            if (!Equals(block.Hash, header.Keccak()))
                return OperatingError.HashMismatched;
            if (block.Header.Index != 0 && header.PrevBlockHash.IsZero())
                return OperatingError.InvalidBlock;
            if (header.MerkleRoot is null)
                return OperatingError.InvalidMerkeRoot;
            var merkleRoot = MerkleTree.ComputeRoot(block.TransactionHashes) ?? UInt256Utils.Zero;
            if (!merkleRoot.Equals(header.MerkleRoot))
                return OperatingError.InvalidMerkeRoot;
            return VerifySignatures(block);
        }

        private OperatingError VerifySignatures(Block? block)
        {
            if (block is null) return OperatingError.InvalidBlock;
            // Setting checkValidatorSet = false because we don't have validator set.
            return _blockManager.VerifySignatures(block, false);
        }

        public static int ExpectedBlockCount(ulong fromBlock, ulong toBlock)
        {
            return (int) (toBlock - fromBlock + 1);
        }

        private void AddToDB()
        {
            while (_downloaded.TryGetValue(_done+1, out var block))
            {
                _repository.AddBlock(block);
                _done++;
                _downloaded.Remove(_done);
            }
    /*        if(_downloaded.Count>0) Console.WriteLine("More blocks _downloaded. Done: " + 
            _done + " _downloaded: "+_downloaded.Count+" NextBlockToDownload " +
            _nextBlocksToDownload.Count+ " pending: "+_pending.Count+" Min pending: "+ _pending.Min()
            +" Min to download: "+_nextBlocksToDownload.Min());
*/
        }

        private void StartVerification()
        {
            if (_verificationRunning) return;
            _verificationRunning = true;
            _blockVerifyThread.Start();
        }

        private void RunBlockVerifier()
        {
            var waitingTime = 1000;
            while (!Done())
            {
                var currentBlock = _lastVerified + 1;
                lock (_blocksToVerify)
                {
                    while (_blocksToVerify.TryGetValue(currentBlock, out var pair))
                    {
                        _blocksToVerify.Remove(currentBlock);
                        var (block, peer) = pair;
                        string peerPubkey = (peer is null) ? "null" : peer.ToHex();
                        var error = VerifyBlock(block);
                        if (error != OperatingError.Ok)
                        {
                            Logger.LogDebug($"Block Verification failed with: {error} from peer {peerPubkey}");
                            _nextBlocksToDownload.Add(currentBlock);
                            break;
                        }
                        var prevBlock = GetBlock(currentBlock - 1);
                        if (prevBlock is null)
                            throw new Exception($"Last verified block {currentBlock - 1} not found");
                        if (!prevBlock.Hash.Equals(block.Header.PrevBlockHash))
                        {
                            Logger.LogDebug($"Previous block hash mismatch for block {currentBlock} from peer {peerPubkey}."
                                + $" Previous block hash {prevBlock.Hash.ToHex()}, current block hash {block.Hash.ToHex()},"
                                + $" previous block hash in current block: {block.Header.PrevBlockHash.ToHex()}");
                            _nextBlocksToDownload.Add(currentBlock);
                            break;
                        }
                        if (currentBlock > _done)
                            _downloaded.TryAdd(currentBlock, block);
                        currentBlock++;
                    }
                }

                currentBlock--;
                if (currentBlock > _lastVerified)
                {
                    Logger.LogInformation($"Verified {currentBlock - _lastVerified} blocks");
                    AddToDB();
                    _lastVerified = currentBlock;
                }
                Thread.Sleep(waitingTime);
            }

            _verificationRunning = false;
            Logger.LogTrace("Block verifier stopped");
        }

        private Block? GetBlock(ulong height)
        {
            if (_downloaded.TryGetValue(height, out var block))
                return block;
            return _repository.BlockByHeight(height);
        }
    }
}
