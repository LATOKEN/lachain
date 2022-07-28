using System;
using System.Collections.Generic;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Config;
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
        private readonly ISnapshotIndexRepository _snapshotIndexer;
        private readonly IConfigManager _configManager;
        private SortedSet<Checkpoint> _checkpoints;
        private SortedSet<ulong> _pending = new SortedSet<ulong>();
        private readonly byte StateHashCount = 6;

        public CheckpointManager(
            IConfigManager configManager,
            IBlockManager blockManager,
            ISnapshotIndexRepository snapshotIndexer
        )
        {
            _configManager = configManager;
            _blockManager = blockManager;
            _snapshotIndexer = snapshotIndexer;
            _blockManager.OnBlockPersisted += OnBlockPersisted;
            _checkpoints = new SortedSet<Checkpoint>(new CheckpointComparer());
        }

        private void OnBlockPersisted(object? sender, Block block)
        {
            var height = block.Header.Index;
            var pendingCheckpoints = _pending.Count;
            while (_pending.Count > 0)
            {
                var blockHeight = _pending.Min;
                if (blockHeight > height)
                    break;
                var checkpointInfo = new CheckpointConfigInfo(blockHeight);
                if (!VerifyCheckpoint(checkpointInfo, out var checkpoint))
                    throw new Exception($"Invalid checkpoint for block {checkpointInfo.BlockHeight}");
                _checkpoints.Add(checkpoint!);
                _pending.Remove(blockHeight);
            }
            if (pendingCheckpoints > _pending.Count)
            {
                _configManager.UpdateCheckpoint(GetAllCheckpoints());
            }
        }

        private bool VerifyCheckpoint(CheckpointConfigInfo checkpointInfo, out Checkpoint? checkpoint)
        {
            checkpoint = null;
            var blockHeight = checkpointInfo.BlockHeight;
            Logger.LogTrace($"Verifying checkpoint info for block height: {blockHeight}");
            var block = _blockManager.GetByHeight(blockHeight);
            if (block is null)
            {
                Logger.LogWarning($"No block found for {blockHeight}");
                return false;
            }

            if (!(checkpointInfo.BlockHash is null) && !block.Hash.Equals(checkpointInfo.BlockHash.HexToUInt256()))
            {
                Logger.LogDebug($"Block hash mismatch for checkpoint block height: {blockHeight}");
                Logger.LogDebug($"We have block hash: {block.Hash.ToHex()}");
                Logger.LogDebug($"Got block hash from config: {checkpointInfo.BlockHash}");
                return false;
            }

            var trieNames = CheckpointUtils.GetAllStateNames();
            var stateHashes = GetStateHashesForBlock(blockHeight);
            if (stateHashes.Count != StateHashCount)
                return false;

            var checkcpointStateHashes = new List<CheckpointStateHash>();
            for (int iter = 0 ; iter < trieNames.Length; iter++)
            {
                var trieName = trieNames[iter];
                var savedStateHash = stateHashes[iter];
                if (checkpointInfo.StateHashes.TryGetValue(trieName, out var stateHash))
                {
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

                var checkpointType = CheckpointUtils.GetCheckpointTypeForSnapshotName(trieName);
                if (checkpointType is null)
                {
                    Logger.LogWarning($"Invalid trieName {trieName}");
                    return false;
                }
                checkcpointStateHashes.Add(
                    new CheckpointStateHash
                    {
                        CheckpointType = ByteString.CopyFrom((byte) checkpointType),
                        StateHash = savedStateHash
                    }
                );
            }
            checkpoint = new Checkpoint
            {
                BlockHeight = blockHeight,
                BlockHash = block.Hash,
                StateHashes = {checkcpointStateHashes}
            };
            return true;
        }

        public void VerifyAndAddCheckpoints(List<CheckpointConfigInfo> checkpoints)
        {
            foreach (var checkpointInfo in checkpoints)
            {
                if (checkpointInfo.BlockHeight > _blockManager.GetHeight())
                {
                    _pending.Add(checkpointInfo.BlockHeight);
                    continue;
                }
                if (!VerifyCheckpoint(checkpointInfo, out var checkpoint))
                    throw new Exception($"Invalid checkpoint for block {checkpointInfo.BlockHeight}");
                _checkpoints.Add(checkpoint!);
            }
        }

        public List<Checkpoint> GetAllCheckpoints()
        {
            var allCheckpoints = new List<Checkpoint>();
            foreach (var checkcpoint in _checkpoints)
            {
                allCheckpoints.Add(new Checkpoint(checkcpoint));
            }
            return allCheckpoints;
        }

        public Checkpoint? GetCheckpoint(ulong height)
        {
            var checkpointToFind = new Checkpoint
            {
                BlockHeight = height
            };
            if (_checkpoints.TryGetValue(checkpointToFind, out var checkpoint))
                return new Checkpoint(checkpoint);
            return null;
        }

        private List<UInt256> GetStateHashesForBlock(ulong height)
        {
            var trieNames = CheckpointUtils.GetAllStateNames();
            var stateHashes = new List<UInt256>();
            try
            {
                var blockchainSnapshot = _snapshotIndexer.GetSnapshotForBlock(height);
                foreach (var trieName in trieNames)
                {
                    var snapshot = blockchainSnapshot.GetSnapshot(trieName);
                    if (snapshot is null)
                    {
                        throw new Exception($"Invalid trieName {trieName}");
                    }
                    stateHashes.Add(snapshot.Hash);
                }
                return stateHashes;
            }
            catch (Exception exception)
            {
                Logger.LogWarning($"Got exception trying to fetch snapshot for block {height}. Exception: {exception}");
                return new List<UInt256>();
            }
        } 

        public UInt256? GetCheckpointBlockHash(ulong blockHeight)
        {
            var checkcpoint = GetCheckpoint(blockHeight);
            return checkcpoint is null ? null : checkcpoint.BlockHash;
        }

        public UInt256? GetStateHashForCheckpointType(CheckpointType checkpointType, ulong blockHeight)
        {
            var checkcpoint = GetCheckpoint(blockHeight);
            if (checkcpoint is null) return null;
            var stateHashes = checkcpoint.StateHashes;
            foreach (var stateHash in stateHashes)
            {
                var type = stateHash.CheckpointType.ToByteArray()[0];
                if ((CheckpointType) type == checkpointType)
                {
                    return stateHash.StateHash;
                }
            }
            return null;
        }

        public UInt256? GetStateHashForSnapshotType(RepositoryType snapshotType, ulong blockHeight)
        {
            var checkpointType = CheckpointUtils.GetCheckpointTypeForSnapshotType(snapshotType);
            if (checkpointType is null) return null;
            return GetStateHashForCheckpointType(checkpointType.Value, blockHeight);
        }

        public UInt256? GetStateHashForSnapshotName(string snapshotName, ulong blockHeight)
        {
            var checkpointType = CheckpointUtils.GetCheckpointTypeForSnapshotName(snapshotName);
            if (checkpointType == null) return null;
            return GetStateHashForCheckpointType(checkpointType.Value, blockHeight);
        }

        public ulong GetMaxHeight()
        {
            ulong maxHeight = 0;
            if (_checkpoints.Count > 0)
                maxHeight = _checkpoints.Max!.BlockHeight;
            return maxHeight;
        }

        public CheckpointInfo GetCheckpointInfo(CheckpointType checkpointType, ulong? height = null)
        {
            if (height is null) height = GetMaxHeight();
            var checkpoint = new CheckpointInfo();
            switch (checkpointType)
            {
                case CheckpointType.BlockHeight:
                    checkpoint.CheckpointBlockHeight = new CheckpointBlockHeight
                    {
                        BlockHeight = height.Value,
                        CheckpointType = ByteString.CopyFrom((byte)checkpointType)
                    };
                    break;

                case CheckpointType.BlockHash:
                    checkpoint.CheckpointBlockHash = new CheckpointBlockHash
                    {
                        BlockHash = GetCheckpointBlockHash(height.Value),
                        CheckpointType = ByteString.CopyFrom((byte)checkpointType)
                    };
                    break;

                case CheckpointType.CheckpointExist:
                    checkpoint.CheckpointExist = new CheckpointExist
                    {
                        Exist = (_checkpoints.Count > 0),
                        CheckpointType = ByteString.CopyFrom((byte)checkpointType)
                    };
                    break;

                default:
                    checkpoint.CheckpointStateHash = new CheckpointStateHash
                    {
                        StateHash = GetStateHashForCheckpointType(checkpointType, height.Value),
                        CheckpointType = ByteString.CopyFrom((byte)checkpointType)
                    };
                    break;
            }

            return checkpoint;
        }

    }
}