using System;
using Lachain.Storage.Repositories;
using Lachain.Utility.Utils;
using Lachain.Logger;
using Lachain.Storage.State;

namespace Lachain.Storage.DbCompact
{
    public class DbShrink : IDbShrink
    {
        private static readonly ILogger<DbShrink> Logger =
            LoggerFactory.GetLoggerForClass<DbShrink>();
        private readonly ISnapshotIndexRepository _snapshotIndexRepository;
        private IDbShrinkRepository _repository;
        private ulong? dbShrinkDepth = null;
        private DbShrinkStatus? dbShrinkStatus = null;
        private ulong? oldestSnapshot = null;

        public DbShrink(ISnapshotIndexRepository snapshotIndexRepository, IDbShrinkRepository repository)
        {
            _snapshotIndexRepository = snapshotIndexRepository;
            _repository = repository;
            dbShrinkStatus = GetDbShrinkStatus();
            dbShrinkDepth = GetDbShrinkDepth();
            oldestSnapshot = GetOldestSnapshotInDb();
        }

        public bool IsStopped()
        {
            return dbShrinkStatus == DbShrinkStatus.Stopped;
        }

        private void SetDbShrinkStatus(DbShrinkStatus status)
        {
            Logger.LogTrace($"Setting db-shrink-status: {status}");
            dbShrinkStatus = status;
            _repository.SetDbShrinkStatus(status);
        }

        public DbShrinkStatus GetDbShrinkStatus()
        {
            if (dbShrinkStatus != null) return dbShrinkStatus.Value;
            return _repository.GetDbShrinkStatus();
        }

        private void SetDbShrinkDepth(ulong depth)
        {
            dbShrinkDepth = depth;
            _repository.SetDbShrinkDepth(depth);
        }

        public ulong? GetDbShrinkDepth()
        {
            if (dbShrinkDepth != null) return dbShrinkDepth;
            return _repository.GetDbShrinkDepth();
        }

        private ulong StartingBlockToKeep(ulong depth, ulong totalBlocks)
        {
            return totalBlocks - depth + 1;
        }

        private ulong GetOldestSnapshotInDb()
        {
            if (oldestSnapshot != null) return oldestSnapshot.Value;
            var block = _repository.GetOldestSnapshotInDb();
            Logger.LogTrace($"Found oldest snapshot block in db: {block}");
            return block;
        }

        private void SetOldestSnapshotInDb(ulong block)
        {
            var currentBlock = GetOldestSnapshotInDb();
            if(block > currentBlock)
            {
                oldestSnapshot = block;
                _repository.SetOldestSnapshotInDb(block);
            }
        }

        private void Stop()
        {
            Logger.LogTrace("Stopping hard db optimization");
            _repository.DeleteStatusAndDepth();
        }

        // consider taking a backup of the folder ChainLachain in case anything goes wrong
        public void ShrinkDb(ulong depth, ulong totalBlocks, bool consistencyCheck)
        {
            if (dbShrinkStatus != DbShrinkStatus.Stopped)
            {
                if (dbShrinkDepth is null)
                {
                    throw new Exception("DbCompact process was started but depth was not written. This should not happen.");
                }
                if (dbShrinkDepth != depth)
                {
                    throw new Exception($"Process was started before with depth {dbShrinkDepth} but was not finished."
                        + $" Got new depth {depth}. Use depth {dbShrinkDepth} and finish the process before "
                        + "using new depth.");
                }
            }
            DbShrinkUtils.ResetCounter();
            switch (dbShrinkStatus)
            {
                case DbShrinkStatus.Stopped:
                    SetDbShrinkDepth(depth);
                    Logger.LogTrace("Starting hard db optimization");
                    Logger.LogTrace($"Keeping latest {depth} snapshots from last approved snapshot" 
                        + $"for blocks: {StartingBlockToKeep(depth, totalBlocks)} to {totalBlocks}");
                    SetDbShrinkStatus(DbShrinkStatus.SaveNodeId);
                    goto case DbShrinkStatus.SaveNodeId;

                case DbShrinkStatus.SaveNodeId:
                    SaveRecentSnapshotNodeId(depth, totalBlocks);
                    SetDbShrinkStatus(DbShrinkStatus.DeleteOldSnapshot);
                    goto case DbShrinkStatus.DeleteOldSnapshot;

                case DbShrinkStatus.DeleteOldSnapshot:
                    Logger.LogTrace($"Deleting nodes from DB that are not reachable from last {depth} snapshots");
                    ulong fromBlock = GetOldestSnapshotInDb(), toBlock = StartingBlockToKeep(depth, totalBlocks) - 1;
                    DeleteOldSnapshot(fromBlock, toBlock);
                    SetDbShrinkStatus(DbShrinkStatus.DeleteNodeId);
                    goto case DbShrinkStatus.DeleteNodeId;

                case DbShrinkStatus.DeleteNodeId:
                    DeleteRecentSnapshotNodeId(depth, totalBlocks);
                    SetDbShrinkStatus(DbShrinkStatus.CheckConsistency);
                    goto case DbShrinkStatus.CheckConsistency;

                case DbShrinkStatus.CheckConsistency:
                    if (consistencyCheck) CheckSnapshots(depth, totalBlocks);
                    Stop();
                    break;
                    
                default:
                    throw new Exception("invalid db-shrink-status");
            }
        }

        private void CheckSnapshots(ulong depth, ulong totalBlocks)
        {
            var fromBlock = StartingBlockToKeep(depth, totalBlocks);
            Logger.LogTrace($"Checking snapshots for blocks in range [{fromBlock} , {totalBlocks}]");
            for (var block = fromBlock; block <= totalBlocks; block++)
            {
                try
                {
                    var blockchainSnapshot = _snapshotIndexRepository.GetSnapshotForBlock(block);
                    var snapshots = blockchainSnapshot.GetAllSnapshot();
                    foreach (var snapshot in snapshots)
                    {
                        if (!snapshot.IsTrieNodeHashesOk())
                        {
                            throw new Exception($"Consistency check failed for {snapshot} of block {block}");
                        }
                    }
                }
                catch(Exception exception)
                {
                    throw new Exception($"Got exception trying to get snapshot for block {block}, "
                        + $"exception:\n{exception}");
                }
            }
        }

        private void DeleteOldSnapshot(ulong fromBlock, ulong toBlock)
        {
            ulong deletedNodes = 0;
            for(ulong block = fromBlock ; block <= toBlock; block++)
            {
                try
                {
                    var blockchainSnapshot = _snapshotIndexRepository.GetSnapshotForBlock(block);
                    var snapshots = blockchainSnapshot.GetAllSnapshot();
                    foreach(var snapshot in snapshots)
                    {
                        var count = snapshot.DeleteSnapshot(_repository);
                        deletedNodes += count;
                    }
                    foreach(var snapshot in snapshots)
                    {
                        _repository.DeleteVersion(snapshot.RepositoryId, block, snapshot.Version);
                        Logger.LogTrace($"Deleted version {snapshot.Version} for "
                            + $"{(RepositoryType) snapshot.RepositoryId} for block {block}");
                    }
                    SetOldestSnapshotInDb(block + 1);
                }
                catch (Exception exception)
                {
                    throw new Exception($"Got exception trying to fetch snapshots for block {block}, probable"
                        + $" reason: last non deleted block is not written in db. Exception:\n{exception}");
                }
            }
            Logger.LogTrace($"Deleted {deletedNodes} nodes from DB in total");
        }

        private void SaveRecentSnapshotNodeId(ulong depth, ulong totalBlocks)
        {
            ulong nodeIdSaved = 0, fromBlock = StartingBlockToKeep(depth, totalBlocks);
            Logger.LogTrace($"Saving nodeId for snapshots in range [{fromBlock}, {totalBlocks}]. All other "
                + "snapshots will be deleted permanently");
            for(var block = fromBlock; block <= totalBlocks; block++)
            {
                try
                {
                    var blockchainSnapshot = _snapshotIndexRepository.GetSnapshotForBlock(block);
                    var snapshots = blockchainSnapshot.GetAllSnapshot();
                    foreach(var snapshot in snapshots)
                    {
                        var count = snapshot.SaveNodeId(_repository);
                        nodeIdSaved += count;
                    }
                }
                catch (Exception exception)
                {
                    throw new Exception($"Got exception trying to fetch snapshots for block {block}, probable"
                        + $" reason: the snapshots were deleted by a previous call. Exception:\n{exception}");
                }
            }
            Logger.LogTrace($"Saved {nodeIdSaved} nodeId in total");
        }

        private void DeleteRecentSnapshotNodeId(ulong depth, ulong totalBlocks)
        {
            ulong nodeIdDeleted = 0, fromBlock = StartingBlockToKeep(depth, totalBlocks);
            Logger.LogTrace($"Deleting nodeId for snapshots in range [{fromBlock}, {totalBlocks}]");
            for(var block = fromBlock; block <= totalBlocks; block++)
            {
                try
                {
                    var blockchainSnapshot = _snapshotIndexRepository.GetSnapshotForBlock(block);
                    var snapshots = blockchainSnapshot.GetAllSnapshot();
                    foreach(var snapshot in snapshots)
                    {
                        var count = snapshot.DeleteNodeId(_repository);
                        nodeIdDeleted += count;
                    }
                }
                catch (Exception exception)
                {
                    throw new Exception($"Got exception trying to fetch snapshots for block {block}, probable"
                        + $" reason: the snapshots were deleted by a previous call. Exception:\n{exception}");
                }
            }
            Logger.LogTrace($"Deleted {nodeIdDeleted} nodeId in total");
        }
    }


}