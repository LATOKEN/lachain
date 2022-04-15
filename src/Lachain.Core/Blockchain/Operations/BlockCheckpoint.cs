using System.Runtime.CompilerServices;
using System.Collections.Generic;
using Lachain.Logger;
using Lachain.Core.Blockchain.Interface;
using Lachain.Proto;
using Lachain.Storage.Repositories;
using Lachain.Storage.State;
using Lachain.Utility.Utils;

namespace Lachain.Core.Blockchain.Operations
{
    public class BlockCheckpoint : IBlockCheckpoint
    {
        private static readonly ILogger<BlockCheckpoint> Logger = LoggerFactory.GetLoggerForClass<BlockCheckpoint>();
        private readonly IBlockManager _blockManager;
        private readonly IBlockCheckpointRepository _repository;
        private readonly ISnapshotIndexRepository _snapshotIndexer;
        private ulong? _checkpointBlockId;
        private UInt256? _checkpointBlockHash;
        private IDictionary<RepositoryType, UInt256?> _stateHashes = new Dictionary<RepositoryType, UInt256?>();
        public ulong? CheckpointBlockId => _checkpointBlockId;
        public UInt256? CheckpointBlockHash => _checkpointBlockHash;
        public BlockCheckpoint(
            IBlockManager blockManager,
            IBlockCheckpointRepository repository,
            ISnapshotIndexRepository snapshotIndexer
        )
        {
            _blockManager = blockManager;
            _repository = repository;
            _snapshotIndexer = snapshotIndexer;
            UpdateCache();
            _blockManager.OnBlockPersisted += UpdateCheckpoint;
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
                SaveCheckpoint(block);
            }
        }

        private bool CheckpointReached(ulong blockId)
        {
            var period = CheckpointPeriod();
            return blockId % period == 0 ;
        }

        public void SaveCheckpoint(Block block)
        {
            _repository.SaveCheckpoint(block);
            UpdateCache();
        }

        private void UpdateCache()
        {
            _checkpointBlockId = _repository.FetchCheckpointBlockId();
            _checkpointBlockHash = _repository.FetchCheckpointBlockHash();

            var snapshotTypes = new List<RepositoryType>();
            snapshotTypes.Add(RepositoryType.BalanceRepository);
            snapshotTypes.Add(RepositoryType.ContractRepository);
            snapshotTypes.Add(RepositoryType.EventRepository);
            snapshotTypes.Add(RepositoryType.StorageRepository);
            snapshotTypes.Add(RepositoryType.TransactionRepository);
            snapshotTypes.Add(RepositoryType.ValidatorRepository);

            _stateHashes = new Dictionary<RepositoryType, UInt256?>();
            foreach (var snapshotType in snapshotTypes)
            {
                var stateHash = _repository.FetchSnapshotStateHash(snapshotType);
                _stateHashes[snapshotType] = stateHash;
            }
        }

        public Block? GetCheckPointBlock()
        {
            if (_checkpointBlockId is null) return null;
            return _blockManager.GetByHeight(_checkpointBlockId.Value);
        }

        public UInt256? GetStateHashForSnapshot(RepositoryType snapshotType)
        {
            if (_stateHashes.TryGetValue(snapshotType, out var stateHash))
            {
                return stateHash;
            }
            return null;
        }

        public bool IsCheckpointConsistent()
        {

            if ( (_checkpointBlockId is null) && (_checkpointBlockHash is null) && _stateHashes.Count == 0 )
            {
                Logger.LogInformation("nothing is saved as checkpoint. "
                    + $"Current block height: {_blockManager.GetHeight()}, checkpoint period: {CheckpointPeriod()}");
                return (_blockManager.GetHeight() <= CheckpointPeriod());
            }

            if (_checkpointBlockId is null)
            {
                Logger.LogInformation("Checkpoint block index is not saved");
                return false;
            }

            if (_checkpointBlockHash is null)
            {
                Logger.LogInformation("Checkpoint block hash is not saved");
                return false;
            }

            var block = GetCheckPointBlock();
            if (block is null)
            {
                Logger.LogInformation($"Found null block for checkpoint block: {_checkpointBlockId}");
                return false;
            }

            if (block.Hash != _checkpointBlockHash)
            {
                Logger.LogInformation($"Block hash mismatch, block hash saved in checkpoint: {_checkpointBlockHash.ToHex()}"
                    + $" actual block hash: {block.Hash.ToHex()}");
                return false;
            }

            var blockchainSnapshot = _snapshotIndexer.GetSnapshotForBlock(_checkpointBlockId.Value);
            var snapshots = blockchainSnapshot.GetAllSnapshot();
            foreach (var snapshot in snapshots)
            {
                var stateHash = GetStateHashForSnapshot((RepositoryType) snapshot.RepositoryId);
                if (stateHash is null)
                {
                    Logger.LogInformation($"State hash is not saved for {(RepositoryType) snapshot.RepositoryId}");
                    return false;
                }
                if (stateHash != snapshot.Hash)
                {
                    Logger.LogInformation($"State hash mismatch for {(RepositoryType) snapshot.RepositoryId}"
                        + $"saved state hash: {stateHash.ToHex()}, actual state hash: {snapshot.Hash.ToHex()}");
                    return false;
                }
            }

            return true;
        }

    }
}