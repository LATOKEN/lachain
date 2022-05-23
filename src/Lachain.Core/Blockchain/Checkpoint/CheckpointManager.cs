using System;
using System.Collections.Generic;
using Lachain.Core.Blockchain.Interface;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Storage.Repositories;
using Lachain.Storage.State;
using Lachain.Utility.Utils;
using Google.Protobuf;

namespace Lachain.Core.Blockchain.Checkpoint
{
    public class CheckpointManager : ICheckpointManager
    {
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
        }

        private bool SaveCheckpoint(ulong blockHeight)
        {
            if (_blockManager.GetHeight() < blockHeight)
            {
                Logger.LogWarning($"We have height: {_blockManager.GetHeight()}, asked to save "
                    + $"checkpoint for block {blockHeight}. Aborting...");
                return false;
            }
            var block = _blockManager.GetByHeight(blockHeight);
            Logger.LogTrace($"Saving checkpoint for block {block!.Header.Index}");
            var saved = _repository.SaveCheckpoint(block);
            if (saved)
            {
                Logger.LogTrace("Saved checkpoint successfully");
            }
            else Logger.LogTrace("Could not save checkpoint");
            return saved;
        }

        public bool CheckOrSaveCheckpoint(CheckpointConfigInfo checkpoint)
        {
            var blockHeight = checkpoint.BlockHeight;
            if (checkpoint.BlockHash is null && checkpoint.StateHashes.Count == 0)
            {
                return SaveCheckpoint(blockHeight);
            }
            Logger.LogTrace($"Checking checkpoint info for block height: {blockHeight}");
            var blockHash = _repository.FetchCheckpointBlockHash(blockHeight);
            if (checkpoint.BlockHash is null || blockHash is null || !blockHash.Equals(checkpoint.BlockHash.HexToUInt256()))
            {
                Logger.LogDebug($"Block hash mismatch for checkpoint block height: {blockHeight}");
                Logger.LogDebug($"Saved block hash: {((blockHash is null) ? "null" : blockHash.ToHex())}");
                Logger.LogDebug($"Got block hash from config: {((checkpoint.BlockHash is null) ? "null" : checkpoint.BlockHash)}");
                return false;
            }
            var trieNames = CheckpointUtils.GetAllStateNames();
            foreach (var trieName in trieNames)
            {
                if (!checkpoint.StateHashes.TryGetValue(trieName, out var stateHash))
                {
                    Logger.LogDebug($"Could not find state hash for {trieName}");
                    return false;
                }
                var repositoryId = CheckpointUtils.GetSnapshotTypeForSnapshotName(trieName);
                if (repositoryId is null) throw new Exception($"Invalid trie name: {trieName}");
                var savedStateHash = _repository.FetchSnapshotStateHash(repositoryId.Value, blockHeight);
                if (savedStateHash is null || !savedStateHash.Equals(stateHash.HexToUInt256()))
                {
                    Logger.LogDebug($"State hash mismatch for snapshot {trieName} and checkpoint block height: {blockHeight}");
                    Logger.LogDebug($"Saved state hash: {((savedStateHash is null) ? "null" : savedStateHash.ToHex())}");
                    Logger.LogDebug($"Got state hash from config: {stateHash}");
                    return false;
                }
            }
            return true;
        }

        private void UpdateCache()
        {
            // TODO: update cache: take the largest checkpoint block height
            // var blockHeights = _repository.FetchCheckpointBlockHeights();

            // _checkpointBlockId = _repository.FetchCheckpointBlockId();
            // _checkpointBlockHash = _repository.FetchCheckpointBlockHash();
            // if (_checkpointBlockId is null) return;
            // var snapshotTypes = new List<RepositoryType>();
            // snapshotTypes.Add(RepositoryType.BalanceRepository);
            // snapshotTypes.Add(RepositoryType.ContractRepository);
            // snapshotTypes.Add(RepositoryType.EventRepository);
            // snapshotTypes.Add(RepositoryType.StorageRepository);
            // snapshotTypes.Add(RepositoryType.TransactionRepository);
            // snapshotTypes.Add(RepositoryType.ValidatorRepository);

            // _stateHashes = new Dictionary<RepositoryType, UInt256?>();
            // foreach (var snapshotType in snapshotTypes)
            // {
            //     var stateHash = _repository.FetchSnapshotStateHash(snapshotType);
            //     _stateHashes[snapshotType] = stateHash;
            // }
        }

        public UInt256? GetCheckPointBlockHash(ulong blockHeight)
        {
            return _repository.FetchCheckpointBlockHash(blockHeight);
        }

        public UInt256? GetStateHashForSnapshotTypeLatest(RepositoryType snapshotType)
        {
            if (_stateHashes.TryGetValue(snapshotType, out var stateHash))
            {
                return stateHash;
            }
            return null;
        }

        public UInt256? GetStateHashForSnapshotType(RepositoryType snapshotType, ulong blockHeight)
        {
            return _repository.FetchSnapshotStateHash(snapshotType, blockHeight);
        }

        public UInt256? GetStateHashForCheckpointTypeLatest(CheckpointType checkpointType)
        {
            var snapshotType = CheckpointUtils.GetSnapshotTypeForCheckpointType(checkpointType);
            if (snapshotType == null) return null;
            return GetStateHashForSnapshotTypeLatest(snapshotType.Value);
        }

        public UInt256? GetStateHashForCheckpointType(CheckpointType checkpointType, ulong blockHeight)
        {
            var snapshotType = CheckpointUtils.GetSnapshotTypeForCheckpointType(checkpointType);
            if (snapshotType == null) return null;
            return GetStateHashForSnapshotType(snapshotType.Value, blockHeight);
        }

        public UInt256? GetStateHashForSnapshotNameLatest(string snapshotName)
        {
            var snapshotType = CheckpointUtils.GetSnapshotTypeForSnapshotName(snapshotName);
            if (snapshotType == null) return null;
            return GetStateHashForSnapshotTypeLatest(snapshotType.Value);
        }

        public UInt256? GetStateHashForSnapshotName(string snapshotName, ulong blockHeight)
        {
            var snapshotType = CheckpointUtils.GetSnapshotTypeForSnapshotName(snapshotName);
            if (snapshotType == null) return null;
            return GetStateHashForSnapshotType(snapshotType.Value, blockHeight);
        }

        public bool IsCheckpointConsistent()
        {
            return true;
            // TODO
            // if ( (_checkpointBlockId is null) && (_checkpointBlockHash is null) && _stateHashes.Count == 0 )
            // {
            //     Logger.LogInformation("nothing is saved as checkpoint.");
            //     return true;
            // }

            // if (_checkpointBlockId is null)
            // {
            //     Logger.LogInformation("Checkpoint block index is not saved");
            //     return false;
            // }

            // if (_checkpointBlockHash is null)
            // {
            //     Logger.LogInformation("Checkpoint block hash is not saved");
            //     return false;
            // }

            // var block = GetCheckPointBlock();
            // if (block is null)
            // {
            //     Logger.LogInformation($"Found null block for checkpoint blockId: {_checkpointBlockId}");
            //     return false;
            // }

            // if (!_checkpointBlockHash.Equals(block.Hash))
            // {
            //     Logger.LogInformation($"Block hash mismatch, block hash saved in checkpoint: {_checkpointBlockHash.ToHex()}"
            //         + $" actual block hash: {block.Hash.ToHex()}");
            //     return false;
            // }

            // var blockchainSnapshot = _snapshotIndexer.GetSnapshotForBlock(_checkpointBlockId.Value);
            // var snapshots = blockchainSnapshot.GetAllSnapshot();
            // foreach (var snapshot in snapshots)
            // {
            //     if (snapshot.RepositoryId == (uint) RepositoryType.BlockRepository) continue;
            //     var stateHash = GetStateHashForSnapshotType((RepositoryType) snapshot.RepositoryId);
            //     if (stateHash is null)
            //     {
            //         Logger.LogInformation($"State hash is not saved for {(RepositoryType) snapshot.RepositoryId}, "
            //             + $"state hash: {snapshot.Hash.ToHex()}");
            //         return false;
            //     }
            //     if (!stateHash.Equals(snapshot.Hash))
            //     {
            //         Logger.LogInformation($"State hash mismatch for {(RepositoryType) snapshot.RepositoryId}"
            //             + $"saved state hash: {stateHash.ToHex()}, actual state hash: {snapshot.Hash.ToHex()}");
            //         return false;
            //     }
            // }

            // return true;
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
                        StateHash = GetStateHashForCheckpointTypeLatest(checkpointType) ?? 
                            throw new NullReferenceException($"Got null state hash for checkpoint: {checkpointType}"),
                        CheckpointType = ByteString.CopyFrom((byte) checkpointType)
                    };
                    break;                
            }
            return checkpoint;
        }

    }
}