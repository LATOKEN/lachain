using System;
using Lachain.Storage.Repositories;
using Lachain.Utility.Utils;
using Lachain.Logger;

namespace Lachain.Storage.DbCompact
{
    public class DbShrink : IDbShrink
    {
        private static readonly ILogger<DbShrink> Logger =
            LoggerFactory.GetLoggerForClass<DbShrink>();
        private readonly ISnapshotIndexRepository _snapshotIndexRepository;
        private IDbShrinkRepository _repository;
        private ulong? dbShrinkDepth;
        private DbShrinkStatus dbShrinkStatus;
        
        public DbShrink(ISnapshotIndexRepository snapshotIndexRepository, IDbShrinkRepository repository)
        {
            _snapshotIndexRepository = snapshotIndexRepository;
            _repository = repository;
            dbShrinkStatus = GetDbShrinkStatus();
            dbShrinkDepth = GetDbShrinkDepth();
        }

        public bool IsStopped()
        {
            return dbShrinkStatus == DbShrinkStatus.Stopped;
        }

        private void SetDbShrinkStatus(DbShrinkStatus status)
        {
            dbShrinkStatus = status;
            var prefix = EntryPrefix.DbShrinkStatus.BuildPrefix();
            var content = new byte[1];
            content[0] = (byte) status;
            _repository.Save(prefix, content, false);
            _repository.Commit();
        }

        public DbShrinkStatus GetDbShrinkStatus()
        {
            var prefix = EntryPrefix.DbShrinkStatus.BuildPrefix();
            var status = _repository.Get(prefix);
            if (status is null) 
            {
                return DbShrinkStatus.Stopped;
            }
            return (DbShrinkStatus) status[0];
        }

        private void SetDbShrinkDepth(ulong depth)
        {
            dbShrinkDepth = depth;
            var prefix = EntryPrefix.DbShrinkDepth.BuildPrefix();
            _repository.Save(prefix, UInt64Utils.ToBytes(depth), false);
            _repository.Commit();
        }

        public ulong? GetDbShrinkDepth()
        {
            var prefix = EntryPrefix.DbShrinkDepth.BuildPrefix();
            var depth = _repository.Get(prefix);
            if (depth is null) return null;
            return UInt64Utils.FromBytes(depth);
        }

        private ulong StartingBlockToKeep(ulong depth, ulong totalBlocks)
        {
            return totalBlocks - depth + 1;
        }

        private ulong GetOldestSnapshotInDb()
        {
            var prefix = EntryPrefix.OldestSnapshotInDb.BuildPrefix();
            var block = _repository.Get(prefix);
            if (block is null) return 0;
            return UInt64Utils.FromBytes(block);
        }

        private void SetOldestSnapshotInDb(ulong block)
        {
            var currentBlock = GetOldestSnapshotInDb();
            if(block > currentBlock)
            {
                var prefix = EntryPrefix.OldestSnapshotInDb.BuildPrefix();
                _repository.Save(prefix, UInt64Utils.ToBytes(block));
            }
        }

        private void Stop()
        {
            var prefix = EntryPrefix.DbShrinkStatus.BuildPrefix();
            _repository.Delete(prefix, false);
            prefix = EntryPrefix.DbShrinkDepth.BuildPrefix();
            _repository.Delete(prefix, false);
            _repository.Commit();
        }

        public void ShrinkDb(ulong depth, ulong totalBlocks)
        {
            if (dbShrinkStatus != DbShrinkStatus.Stopped)
            {
                if (dbShrinkDepth is null)
                {
                    Logger.LogDebug("DbCompact process was started but depth was not written. This should not happen.");
                    SetDbShrinkDepth(depth);
                }
                if (dbShrinkDepth != depth)
                {
                    Logger.LogDebug($"Process was started before with depth {dbShrinkDepth} but was not finished."
                        + $" Got new depth {depth}. Use depth {dbShrinkDepth} and finish the process before "
                        + "using new depth.");
                }
                return;
            }
            DbShrinkUtils.ResetCounter();
            switch (dbShrinkStatus)
            {
                case DbShrinkStatus.Stopped:
                    SetDbShrinkDepth(depth);
                    SetDbShrinkStatus(DbShrinkStatus.SaveNodeId);
                    goto case DbShrinkStatus.SaveNodeId;

                case DbShrinkStatus.SaveNodeId:
                    Logger.LogTrace("Saving nodeId for recent snapshots");
                    Logger.LogTrace($"Keeping latest {depth} snapshots from last approved snapshot" 
                        + $"for blocks: {StartingBlockToKeep(depth, totalBlocks)} to {totalBlocks}");
                    UpdateNodeIdToBatch(depth, totalBlocks, true);
                    SetDbShrinkStatus(DbShrinkStatus.DeleteOldSnapshot);
                    goto case DbShrinkStatus.DeleteOldSnapshot;

                case DbShrinkStatus.DeleteOldSnapshot:
                    Logger.LogTrace($"Deleting nodes from DB that are not reachable from last {depth} snapshots");
                    ulong fromBlock = GetOldestSnapshotInDb(), toBlock = StartingBlockToKeep(depth, totalBlocks) - 1;
                    DeleteOldSnapshot(fromBlock, toBlock);
                    SetDbShrinkStatus(DbShrinkStatus.DeleteNodeId);
                    goto case DbShrinkStatus.DeleteNodeId;

                case DbShrinkStatus.DeleteNodeId:
                    UpdateNodeIdToBatch(depth, totalBlocks, false);
                    Stop();
                    break;
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
                        _snapshotIndexRepository.DeleteVersion(snapshot.RepositoryId, block, snapshot.Version, _repository);
                    }
                    SetOldestSnapshotInDb(block + 1);
                }
                catch (Exception exception)
                {
                    Logger.LogDebug($"Got exception trying to fetch snapshots for block {block}: {exception}, "
                        + "probable reason: last non deleted block is not written in db.");
                }
            }
            Logger.LogTrace($"Deleted {deletedNodes} nodes from DB in total");
        }

        private void UpdateNodeIdToBatch(ulong depth, ulong totalBlocks, bool save)
        {
            (string Doing, string Done) action = save ? ("Saving" , "Saved") : ("Deleting" , "Deleted");
            Logger.LogTrace($"{action.Doing} nodeId");
            ulong usefulNodes = 0, fromBlock = StartingBlockToKeep(depth, totalBlocks);
            for(var block = fromBlock; block <= totalBlocks; block++)
            {
                try
                {
                    var blockchainSnapshot = _snapshotIndexRepository.GetSnapshotForBlock(block);
                    var snapshots = blockchainSnapshot.GetAllSnapshot();
                    foreach(var snapshot in snapshots)
                    {
                        var count = snapshot.UpdateNodeIdToBatch(save, _repository);
                        usefulNodes += count;
                    }
                }
                catch (Exception exception)
                {
                    Logger.LogDebug($"Got exception trying to fetch snapshots for block {block}: {exception}, "
                        + "probable reason: the snapshots were deleted by a previous call");
                }
            }
            Logger.LogTrace($"{action.Done} {usefulNodes} nodeId in total");
        }
    }


}