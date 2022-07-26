using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
        private Checkpoint? _checkpoint = null;
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
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void OnBlockPersisted(object? sender, Block block)
        {
            var height = block.Header.Index;
            ulong heightToUpdate = 0;
            while (_pending.Count > 0)
            {
                var blockHeight = _pending.Min;
                if (blockHeight > height)
                    break;

                if (heightToUpdate < blockHeight)
                    heightToUpdate = blockHeight;

                _pending.Remove(blockHeight);
            }

            if (heightToUpdate != 0)
            {
                var checkpointInfo = new CheckpointConfigInfo(heightToUpdate);
                if (!VerifyCheckpoint(checkpointInfo, out var checkpoint))
                    throw new Exception($"Invalid checkpoint for block {checkpointInfo.BlockHeight}");
                _checkpoint = checkpoint!;
                _configManager.UpdateCheckpoint(GetCheckpoint()!);
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

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void VerifyAndAddCheckpoints(List<CheckpointConfigInfo> checkpoints)
        {
            var maxCheckpoint = new CheckpointConfigInfo(0);
            foreach (var checkpointInfo in checkpoints)
            {
                if (checkpointInfo.BlockHeight > _blockManager.GetHeight())
                {
                    _pending.Add(checkpointInfo.BlockHeight);
                    continue;
                }
                else if (checkpointInfo.BlockHeight > maxCheckpoint.BlockHeight)
                {
                    maxCheckpoint = checkpointInfo;
                }
            }

            if (maxCheckpoint.BlockHeight != 0)
            {
                if (!VerifyCheckpoint(maxCheckpoint, out var checkpoint))
                    throw new Exception($"Invalid checkpoint for block {maxCheckpoint.BlockHeight}");
                _checkpoint = checkpoint!;
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public Checkpoint? GetCheckpoint()
        {
            if (_checkpoint is null)
                return null;
            return new Checkpoint(_checkpoint);
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

        [MethodImpl(MethodImplOptions.Synchronized)]
        public ulong GetCheckpointBlockHeight()
        {
            var checkcpoint = GetCheckpoint();
            return checkcpoint is null ? 0 : checkcpoint.BlockHeight;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public UInt256? GetCheckpointBlockHash()
        {
            var checkcpoint = GetCheckpoint();
            return checkcpoint is null ? null : checkcpoint.BlockHash;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public UInt256? GetStateHashForCheckpointType(CheckpointType checkpointType)
        {
            var checkcpoint = GetCheckpoint();
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

        [MethodImpl(MethodImplOptions.Synchronized)]
        public UInt256? GetStateHashForSnapshotType(RepositoryType snapshotType)
        {
            var checkpointType = CheckpointUtils.GetCheckpointTypeForSnapshotType(snapshotType);
            if (checkpointType is null) return null;
            return GetStateHashForCheckpointType(checkpointType.Value);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public UInt256? GetStateHashForSnapshotName(string snapshotName)
        {
            var checkpointType = CheckpointUtils.GetCheckpointTypeForSnapshotName(snapshotName);
            if (checkpointType == null) return null;
            return GetStateHashForCheckpointType(checkpointType.Value);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public CheckpointInfo GetCheckpointInfo(CheckpointType checkpointType)
        {
            if (height is null) height = GetMaxHeight();
            var checkpoint = new CheckpointInfo();
            switch (checkpointType)
            {
                case CheckpointType.BlockHeight:
                    checkpoint.CheckpointBlockHeight = new CheckpointBlockHeight
                    {
                        BlockHeight = GetCheckpointBlockHeight(),
                        CheckpointType = ByteString.CopyFrom((byte)checkpointType)
                    };
                    break;

                case CheckpointType.BlockHash:
                    checkpoint.CheckpointBlockHash = new CheckpointBlockHash
                    {
                        BlockHash = GetCheckpointBlockHash(),
                        CheckpointType = ByteString.CopyFrom((byte)checkpointType)
                    };
                    break;

                case CheckpointType.CheckpointExist:
                    checkpoint.CheckpointExist = new CheckpointExist
                    {
                        Exist = !(_checkpoint is null),
                        CheckpointType = ByteString.CopyFrom((byte)checkpointType)
                    };
                    break;

                default:
                    checkpoint.CheckpointStateHash = new CheckpointStateHash
                    {
                        StateHash = GetStateHashForCheckpointType(checkpointType),
                        CheckpointType = ByteString.CopyFrom((byte)checkpointType)
                    };
                    break;
            }

            return checkpoint;
        }

    }
}