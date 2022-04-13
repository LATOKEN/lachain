using System.Runtime.CompilerServices;
using Lachain.Logger;
using Lachain.Core.Blockchain.Interface;
using Lachain.Proto;
using Lachain.Storage.Repositories;

namespace Lachain.Core.Blockchain.Operations
{
    public class BlockCheckpoint : IBlockCheckpoint
    {
        private static readonly ILogger<BlockCheckpoint> Logger = LoggerFactory.GetLoggerForClass<BlockCheckpoint>();
        private readonly IBlockManager _blockManager;
        private readonly IBlockCheckpointRepository _repository;
        private ulong _checkpointBlockId;
        public ulong CheckpointBlockId => _checkpointBlockId;
        public BlockCheckpoint(
            IBlockManager blockManager,
            IBlockCheckpointRepository repository
        )
        {
            _blockManager = blockManager;
            _repository = repository;
            _blockManager.OnBlockPersisted += UpdateCheckpoint;
            _checkpointBlockId = FetchCheckpointBlockId();
        }

        // if the period is p, then the checkpoint will be updated after each p blocks;
        public ulong CheckpointPeriod()
        {
            return 1000000;
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void UpdateCheckpoint(object? sender, Block block)
        {
            if (CheckpointReached(block.Header.Index))
            {
                SaveCheckpointBlock(block);
            }
        }

        private bool CheckpointReached(ulong blockId)
        {
            var period = CheckpointPeriod();
            return blockId % period == 0 ;
        }

        public void SaveCheckpointBlock(Block block)
        {
            _checkpointBlockId = block.Header.Index;
            _repository.SaveCheckpoint(block);
        }
        private ulong FetchCheckpointBlockId()
        {
            return _repository.FetchCheckpointBlockId();
        }
        public Block? FetchCheckPointBlock()
        {
            return _blockManager.GetByHeight(_checkpointBlockId);
        }
    }
}