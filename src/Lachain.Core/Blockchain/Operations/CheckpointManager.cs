using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Lachain.Core.Blockchain.Interface;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Storage.Repositories;
using Lachain.Storage.State;
using Lachain.Utility.Utils;
using Google.Protobuf;

namespace Lachain.Core.Blockchain.Operations
{
    public class CheckpointManager : ICheckpointManager
    {
        public static readonly uint _checkpointPeriod = 1000000; // 10^6
        private static readonly ILogger<CheckpointManager> Logger = LoggerFactory.GetLoggerForClass<CheckpointManager>();
        private readonly IBlockManager _blockManager;
        private readonly ICheckpointRepository _repository;
        private readonly ISnapshotIndexRepository _snapshotIndexer;
        private ulong? _checkpointBlockId;
        private UInt256? _checkpointBlockHash;
        private IDictionary<RepositoryType, UInt256?> _stateHashes = new Dictionary<RepositoryType, UInt256?>();
        public ulong? CheckpointBlockId => _checkpointBlockId;
        public UInt256? CheckpointBlockHash => _checkpointBlockHash;
        public CheckpointManager(
            IBlockManager blockManager,
            ICheckpointRepository repository,
            ISnapshotIndexRepository snapshotIndexer
        )
        {
            _blockManager = blockManager;
            _repository = repository;
            _snapshotIndexer = snapshotIndexer;
            UpdateCache();
            _blockManager.OnBlockPersisted += UpdateCheckpoint;
        }

        public static ulong GetNextCheckpointHeight(ulong height)
        {
            return _checkpointPeriod * (height / _checkpointPeriod + 1);
        }

        public static ulong GetClosestCheckpointHeight(ulong height)
        {
            return _checkpointPeriod * (height / _checkpointPeriod);
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void UpdateCheckpoint(object? sender, Block block)
        {
            if (CheckpointReached(block.Header.Index))
            {
                SaveCheckpoint(block);
            }
        }

        // Check if checkpoint reached. No need to save checkpoint for genesis block
        private bool CheckpointReached(ulong blockId)
        {
            return blockId > 0 && blockId % _checkpointPeriod == 0 ;
        }

        public bool SaveCheckpoint(Block block)
        {
            if (_checkpointBlockId >= block.Header.Index)
            {
                Logger.LogWarning($"We have checkpoint block {_checkpointBlockId}, trying to save checkpoint block "
                    + $"{block.Header.Index}. Aborting save.");
                return false;
            }
            Logger.LogTrace($"Saving checkpoint for block {block.Header.Index}");
            var saved = _repository.SaveCheckpoint(block);
            if (saved)
            {
                Logger.LogTrace("Saved checkpoint successfully");
                UpdateCache();
            }
            else Logger.LogTrace("Could not save checkpoint");
            return saved;
        }

        private void UpdateCache()
        {
            _checkpointBlockId = _repository.FetchCheckpointBlockId();
            _checkpointBlockHash = _repository.FetchCheckpointBlockHash();
            if (_checkpointBlockId is null) return;
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

        public UInt256? GetStateHashForSnapshotType(RepositoryType snapshotType)
        {
            if (_stateHashes.TryGetValue(snapshotType, out var stateHash))
            {
                return stateHash;
            }
            return null;
        }

        public UInt256? GetStateHashForCheckpointType(CheckpointType checkpointType)
        {
            var snapshotType = CheckpointUtils.GetSnapshotTypeForCheckpointType(checkpointType);
            if (snapshotType == null) return null;
            return GetStateHashForSnapshotType(snapshotType.Value);
        }

        public UInt256? GetStateHashForSnapshotName(string snapshotName)
        {
            var snapshotType = CheckpointUtils.GetSnapshotTypeForSnapshotName(snapshotName);
            if (snapshotType == null) return null;
            return GetStateHashForSnapshotType(snapshotType.Value);
        }

        public bool IsCheckpointConsistent()
        {

            if ( (_checkpointBlockId is null) && (_checkpointBlockHash is null) && _stateHashes.Count == 0 )
            {
                Logger.LogInformation("nothing is saved as checkpoint. "
                    + $"Current block height: {_blockManager.GetHeight()}, checkpoint period: {_checkpointPeriod}");
                return (_blockManager.GetHeight() <= _checkpointPeriod);
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
                Logger.LogInformation($"Found null block for checkpoint blockId: {_checkpointBlockId}");
                return false;
            }

            if (!_checkpointBlockHash.Equals(block.Hash))
            {
                Logger.LogInformation($"Block hash mismatch, block hash saved in checkpoint: {_checkpointBlockHash.ToHex()}"
                    + $" actual block hash: {block.Hash.ToHex()}");
                return false;
            }

            var blockchainSnapshot = _snapshotIndexer.GetSnapshotForBlock(_checkpointBlockId.Value);
            var snapshots = blockchainSnapshot.GetAllSnapshot();
            foreach (var snapshot in snapshots)
            {
                if (snapshot.RepositoryId == (uint) RepositoryType.BlockRepository) continue;
                var stateHash = GetStateHashForSnapshotType((RepositoryType) snapshot.RepositoryId);
                if (stateHash is null)
                {
                    Logger.LogInformation($"State hash is not saved for {(RepositoryType) snapshot.RepositoryId}, "
                        + $"state hash: {snapshot.Hash.ToHex()}");
                    return false;
                }
                if (!stateHash.Equals(snapshot.Hash))
                {
                    Logger.LogInformation($"State hash mismatch for {(RepositoryType) snapshot.RepositoryId}"
                        + $"saved state hash: {stateHash.ToHex()}, actual state hash: {snapshot.Hash.ToHex()}");
                    return false;
                }
            }

            return true;
        }

        public CheckpointInfo GetCheckpointInfo(CheckpointType checkpointType)
        {
            var checkpoint = new CheckpointInfo();
            switch (checkpointType)
            {
                case CheckpointType.BlockHeight:
                    checkpoint.CheckpointBlockHeight = new CheckpointBlockHeight
                    {
                        BlockHeight = CheckpointBlockId!.Value,
                        CheckpointType = ByteString.CopyFrom((byte) checkpointType)
                    };
                    break;
                
                case CheckpointType.BlockHash:
                    checkpoint.CheckpointBlockHash = new CheckpointBlockHash
                    {
                        BlockHash = CheckpointBlockHash,
                        CheckpointType = ByteString.CopyFrom((byte) checkpointType)
                    };
                    break;

                case CheckpointType.CheckpointExist:
                    checkpoint.CheckpointExist = new CheckpointExist
                    {
                        Exist = ( IsCheckpointConsistent() && CheckpointBlockId != null ),
                        CheckpointType = ByteString.CopyFrom((byte) checkpointType)
                    };
                    break;

                default:
                    checkpoint.CheckpointStateHash = new CheckpointStateHash
                    {
                        StateHash = GetStateHashForCheckpointType(checkpointType) ?? 
                            throw new NullReferenceException($"Got null state hash for checkpoint: {checkpointType}"),
                        CheckpointType = ByteString.CopyFrom((byte) checkpointType)
                    };
                    break;                
            }
            return checkpoint;
        }

    }
}