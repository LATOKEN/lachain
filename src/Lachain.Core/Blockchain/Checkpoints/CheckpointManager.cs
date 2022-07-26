using System;
using System.Collections.Generic;
using Lachain.Core.Blockchain.Interface;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Storage.Repositories;
using Lachain.Storage.State;
using Lachain.Utility.Utils;
using Google.Protobuf;

namespace Lachain.Core.Blockchain.Checkpoints
{
    public class CheckpointManager : ICheckpointManager
    {
        private static readonly ILogger<CheckpointManager>
            Logger = LoggerFactory.GetLoggerForClass<CheckpointManager>();

        private readonly IBlockManager _blockManager;
        private readonly ICheckpointRepository _repository;
        private readonly ISnapshotIndexRepository _snapshotIndexer;
        private ulong? _checkpointBlockHeight;
        private UInt256? _checkpointBlockHash;
        private IDictionary<RepositoryType, UInt256?> _stateHashes = new Dictionary<RepositoryType, UInt256?>();
        public ulong? CheckpointBlockHeight => _checkpointBlockHeight;
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
            Logger.LogTrace($"Trying to save checkpoint for block {block!.Header.Index}");
            var saved = _repository.SaveCheckpoint(block);
            if (saved)
            {
                Logger.LogTrace("Saved checkpoint successfully");
            }
            else Logger.LogTrace("Could not save checkpoint");

            return saved;
        }

        private bool CheckOrSaveCheckpoint(CheckpointConfigInfo checkpoint)
        {
            var blockHeight = checkpoint.BlockHeight;
            var blockHash = _repository.FetchCheckpointBlockHash(blockHeight);
            if (blockHash is null)
                return SaveCheckpoint(blockHeight);

            Logger.LogTrace($"Checking checkpoint info for block height: {blockHeight}");
            if (!(checkpoint.BlockHash is null) && !blockHash.Equals(checkpoint.BlockHash.HexToUInt256()))
            {
                Logger.LogDebug($"Block hash mismatch for checkpoint block height: {blockHeight}");
                Logger.LogDebug($"Saved block hash: {blockHash.ToHex()}");
                Logger.LogDebug($"Got block hash from config: {checkpoint.BlockHash}");
                return false;
            }

            var trieNames = CheckpointUtils.GetAllStateNames();
            foreach (var trieName in trieNames)
            {
                if (checkpoint.StateHashes.TryGetValue(trieName, out var stateHash))
                {
                    var repositoryId = CheckpointUtils.GetSnapshotTypeForSnapshotName(trieName);
                    if (repositoryId is null)
                        throw new Exception($"Invalid trie name: {trieName}");
                    var savedStateHash = _repository.FetchSnapshotStateHash(repositoryId.Value, blockHeight);
                    if (savedStateHash is null || !savedStateHash.Equals(stateHash.HexToUInt256()))
                    {
                        Logger.LogDebug(
                            $"State hash mismatch for snapshot {trieName} and checkpoint block height: {blockHeight}");
                        Logger.LogDebug(
                            $"Saved state hash: {((savedStateHash is null) ? "null" : savedStateHash.ToHex())}");
                        Logger.LogDebug($"Got state hash from config: {stateHash}");
                        return false;
                    }
                }
            }

            return true;
        }

        public void AddCheckpoints(List<CheckpointConfigInfo> checkpoints)
        {
            foreach (var checkpoint in checkpoints)
            {
                if (!CheckOrSaveCheckpoint(checkpoint))
                    throw new Exception($"Invalid checkpoint for block {checkpoint.BlockHeight}");
            }

            UpdateCache();
        }

        private void UpdateCache()
        {
            var blockHeights = _repository.FetchCheckpointBlockHeights();
            if (blockHeights.Count == 0) return;
            ulong maxHeight = 0;
            foreach (var height in blockHeights)
            {
                if (height > maxHeight)
                    maxHeight = height;
            }

            _checkpointBlockHeight = maxHeight;
            _checkpointBlockHash = _repository.FetchCheckpointBlockHash(maxHeight);
            var trieNames = CheckpointUtils.GetAllStateNames();
            _stateHashes = new Dictionary<RepositoryType, UInt256?>();
            foreach (var trieName in trieNames)
            {
                var snapshotType = CheckpointUtils.GetSnapshotTypeForSnapshotName(trieName);
                if (snapshotType is null)
                    throw new Exception($"Invalid trie name: {trieName}");
                var stateHash = _repository.FetchSnapshotStateHash(snapshotType.Value, maxHeight);
                _stateHashes[snapshotType.Value] = stateHash;
            }
        }

        public List<CheckpointConfigInfo> GetAllSavedCheckpoint()
        {
            var blockHeights = _repository.FetchCheckpointBlockHeights();
            var checkpoints = new List<CheckpointConfigInfo>();
            foreach (var height in blockHeights)
            {
                var blockHash = _repository.FetchCheckpointBlockHash(height);
                if (blockHash is null)
                    throw new Exception($"Block hash for block {height} not found");
                IDictionary<string, string> stateHashes = new Dictionary<string, string>();
                var trieNames = CheckpointUtils.GetAllStateNames();
                foreach (var trieName in trieNames)
                {
                    var snapshotType = CheckpointUtils.GetSnapshotTypeForSnapshotName(trieName);
                    if (snapshotType is null)
                        throw new Exception($"Invalid trie name: {trieName}");
                    var stateHash = _repository.FetchSnapshotStateHash(snapshotType.Value, height);
                    if (stateHash is null)
                        throw new Exception($"State hash for snapshot {trieName} for block {height} not found");
                    stateHashes[trieName] = stateHash.ToHex();
                }

                checkpoints.Add(
                    new CheckpointConfigInfo(height, blockHash.ToHex(), stateHashes)
                );
            }

            return checkpoints;
        }

        public UInt256? GetCheckPointBlockHash(ulong blockHeight)
        {
            return _repository.FetchCheckpointBlockHash(blockHeight);
        }

        public UInt256? GetStateHashForSnapshotType(RepositoryType snapshotType, ulong blockHeight)
        {
            if (blockHeight != _checkpointBlockHeight)
                return _repository.FetchSnapshotStateHash(snapshotType, blockHeight);
            if (_stateHashes.TryGetValue(snapshotType, out var stateHash))
            {
                return stateHash;
            }

            return null;
        }

        public UInt256? GetStateHashForCheckpointType(CheckpointType checkpointType, ulong blockHeight)
        {
            var snapshotType = CheckpointUtils.GetSnapshotTypeForCheckpointType(checkpointType);
            if (snapshotType == null) return null;
            return GetStateHashForSnapshotType(snapshotType.Value, blockHeight);
        }

        public UInt256? GetStateHashForSnapshotName(string snapshotName, ulong blockHeight)
        {
            var snapshotType = CheckpointUtils.GetSnapshotTypeForSnapshotName(snapshotName);
            if (snapshotType == null) return null;
            return GetStateHashForSnapshotType(snapshotType.Value, blockHeight);
        }

        public bool IsCheckpointConsistent()
        {
            var blockHeights = _repository.FetchCheckpointBlockHeights();
            foreach (var height in blockHeights)
            {
                var blockHash = _repository.FetchCheckpointBlockHash(height);
                if (blockHash is null)
                {
                    Logger.LogInformation("Checkpoint block hash is not saved");
                    return false;
                }

                var block = _blockManager.GetByHeight(height);
                if (block is null)
                {
                    Logger.LogInformation($"Found null block for checkpoint height: {height}");
                    return false;
                }

                if (!blockHash.Equals(block.Hash))
                {
                    Logger.LogInformation($"Block hash mismatch, block hash saved in checkpoint: {blockHash.ToHex()}"
                                          + $" actual block hash: {block.Hash.ToHex()}");
                    return false;
                }

                try
                {
                    var blockchainSnapshot = _snapshotIndexer.GetSnapshotForBlock(height);
                    var snapshots = blockchainSnapshot.GetAllSnapshot();
                    foreach (var snapshot in snapshots)
                    {
                        if (snapshot.RepositoryId == (uint)RepositoryType.BlockRepository) continue;
                        var stateHash = GetStateHashForSnapshotType((RepositoryType)snapshot.RepositoryId, height);
                        if (stateHash is null)
                        {
                            Logger.LogInformation(
                                $"State hash is not saved for {(RepositoryType)snapshot.RepositoryId}, "
                                + $"state hash: {snapshot.Hash.ToHex()}");
                            return false;
                        }

                        if (!stateHash.Equals(snapshot.Hash))
                        {
                            Logger.LogInformation($"State hash mismatch for {(RepositoryType)snapshot.RepositoryId}"
                                                  + $"saved state hash: {stateHash.ToHex()}, actual state hash: {snapshot.Hash.ToHex()}");
                            return false;
                        }
                    }
                }
                catch (Exception exception)
                {
                    Logger.LogWarning(
                        $"Got exception trying to get snapshot for block: {height}, exception: {exception}");
                    // TODO
                    // It can happen due to error or due to deletion of old snapshots
                    // It can be checked if it is due to deletion of old snapshots
                    // The method for this is available in 'dev' branch, but not in this branch
                    // So it should be added here before merging to 'dev' branch and return false if it is due to error
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
                        BlockHeight = _checkpointBlockHeight!.Value,
                        CheckpointType = ByteString.CopyFrom((byte)checkpointType)
                    };
                    break;

                case CheckpointType.BlockHash:
                    checkpoint.CheckpointBlockHash = new CheckpointBlockHash
                    {
                        BlockHash = _checkpointBlockHash,
                        CheckpointType = ByteString.CopyFrom((byte)checkpointType)
                    };
                    break;

                case CheckpointType.CheckpointExist:
                    checkpoint.CheckpointExist = new CheckpointExist
                    {
                        Exist = (IsCheckpointConsistent() && _checkpointBlockHeight != null),
                        CheckpointType = ByteString.CopyFrom((byte)checkpointType)
                    };
                    break;

                default:
                    checkpoint.CheckpointStateHash = new CheckpointStateHash
                    {
                        StateHash = GetStateHashForCheckpointType(checkpointType, _checkpointBlockHeight!.Value),
                        CheckpointType = ByteString.CopyFrom((byte)checkpointType)
                    };
                    break;
            }

            return checkpoint;
        }

    }
}