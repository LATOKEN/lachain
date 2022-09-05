using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly object _deletionWorker = new object();

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
            _repository.UpdateTime();
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
            var timePassed = _repository.TimePassed();
            var hours = timePassed / (3600 * 1000);
            var minutes = (timePassed % (3600 * 1000)) / (60 * 1000);
            var seconds = timePassed / 1000.0 - hours * 3600 - minutes * 60;
            Logger.LogInformation($"Time took to clean db {hours}h {minutes}m {seconds}s");
            _repository.DeleteAll();
        }

        private bool CheckIfDbShrinkNecessary(ulong depth, ulong totalBlocks)
        {
            if(depth > totalBlocks)
            {
                Logger.LogTrace($"total blocks are {totalBlocks} and got depth {depth}");
                return false;
            }
            if(StartingBlockToKeep(depth, totalBlocks) <= GetOldestSnapshotInDb())
            {
                Logger.LogTrace("No redundant snapshots found in db");
                return false;
            }
            return true;
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
                    if(!CheckIfDbShrinkNecessary(depth, totalBlocks))
                    {
                        Logger.LogTrace("Nothing to delete.");
                        return;
                    }
                    SetDbShrinkDepth(depth);
                    Logger.LogTrace("Starting hard db optimization");
                    Logger.LogTrace($"Keeping latest {depth} snapshots from last approved snapshot" 
                        + $"for blocks: {StartingBlockToKeep(depth, totalBlocks)} to {totalBlocks}");
                    SetDbShrinkStatus(DbShrinkStatus.SaveTempNodeInfo);
                    goto case DbShrinkStatus.SaveTempNodeInfo;

                case DbShrinkStatus.SaveTempNodeInfo:
                    SaveRecentSnapshotNodeIdAndHash(depth, totalBlocks);
                    SetDbShrinkStatus(DbShrinkStatus.DeleteOldSnapshot);
                    goto case DbShrinkStatus.DeleteOldSnapshot;

                case DbShrinkStatus.DeleteOldSnapshot:
                    Logger.LogTrace($"Deleting nodes from DB that are not reachable from last {depth} snapshots");
                    var lastBlock = StartingBlockToKeep(depth, totalBlocks);
                    DeleteOldSnapshot(lastBlock);
                    SetDbShrinkStatus(DbShrinkStatus.DeleteTempNodeInfo);
                    goto case DbShrinkStatus.DeleteTempNodeInfo;

                case DbShrinkStatus.DeleteTempNodeInfo:
                    DeleteRecentSnapshotNodeIdAndHash(depth, totalBlocks);
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

        private void DeleteOldSnapshot(ulong lastBlock)
        {
            Task.Factory.StartNew(() =>
            {
                DeleteNodeById();
            }, TaskCreationOptions.LongRunning);

            Task.Factory.StartNew(() =>
            {
                DeleteNodeIdByHash();
            }, TaskCreationOptions.LongRunning);

            lock (_deletionWorker)
            {
                dbShrinkStatus = DbShrinkStatus.AsyncDeletionStarted; 
                Monitor.Wait(_deletionWorker);
            }

            var repos = Enum.GetValues(typeof(RepositoryType)).Cast<RepositoryType>();
            for (var fromBlock = GetOldestSnapshotInDb(); fromBlock < lastBlock; fromBlock++)
            {
                foreach (var repo in repos)
                {
                    if (repo != RepositoryType.MetaRepository)
                        _repository.DeleteVersion((uint) repo, fromBlock);
                }
                SetOldestSnapshotInDb(fromBlock + 1);
            }
        }

        private void DeleteNodeById()
        {
            Logger.LogTrace($"Deleting nodes of old snapshot from DB");
            var prefixToDelete = EntryPrefix.PersistentHashMap.BuildPrefix();
            var prefixToKeep = EntryPrefix.NodeIdForRecentSnapshot.BuildPrefix();
            var deleted = DeleteOldKeys(prefixToDelete, prefixToKeep);
            Logger.LogTrace($"Deleted {deleted} nodes of old snapshot from DB in total");
            NotifyCaller();
        }

        private void DeleteNodeIdByHash()
        {
            Logger.LogTrace($"Deleting nodes of old snapshot from DB");
            var prefixToDelete = EntryPrefix.VersionByHash.BuildPrefix();
            var prefixToKeep = EntryPrefix.NodeHashForRecentSnapshot.BuildPrefix();
            var deleted = DeleteOldKeys(prefixToDelete, prefixToKeep);
            Logger.LogTrace($"Deleted {deleted} nodes of old snapshot from DB in total");
            NotifyCaller();
        }

        private ulong DeleteOldKeys(byte[] prefixToDelete, byte[] prefixToKeep)
        {
            var ptrToDelete = _repository.GetIteratorForPrefixOnly(prefixToDelete);
            var ptrToKeep = _repository.GetIteratorForPrefixOnly(prefixToKeep);
            if (ptrToDelete is null || !ptrToDelete.Valid()) return 0;
            if (ptrToKeep is null || !ptrToKeep.Valid())
                throw new Exception("Something went wrong, saved nodeId or nodeHash iterator is null or invalid");
            
            ulong keyDeleted = 0;
            var keyToDelete = ptrToDelete.Key().Skip(2).ToArray();
            var keyToKeep = ptrToKeep.Key().Skip(2).ToArray();
            var comparer = new ByteKeyComparer();

            // pointers fetch keys in sorted order, so we can compare them with a loop
            while (ptrToDelete.Valid() && ptrToKeep.Valid())
            {
                var comparison = comparer.Compare(keyToDelete, keyToKeep);
                if (comparison < 0)
                {
                    keyDeleted++;
                    _repository.Delete(ptrToDelete.Key());
                    ptrToDelete.Next();
                    if (ptrToDelete.Valid())
                        keyToDelete = ptrToDelete.Key().Skip(2).ToArray();
                }
                else if (comparison == 0)
                {
                    ptrToDelete.Next();
                    ptrToKeep.Next();
                    if (ptrToDelete.Valid())
                        keyToDelete = ptrToDelete.Key().Skip(2).ToArray();
                    if (ptrToKeep.Valid())
                        keyToKeep = ptrToKeep.Key().Skip(2).ToArray();
                }
                else
                {
                    ptrToKeep.Next();
                    if (ptrToKeep.Valid())
                        keyToKeep = ptrToKeep.Key().Skip(2).ToArray();
                }
            }

            while (ptrToDelete.Valid())
            {
                keyDeleted++;
                _repository.Delete(ptrToDelete.Key());
                ptrToDelete.Next();
                if (ptrToDelete.Valid())
                    keyToDelete = ptrToDelete.Key().Skip(2).ToArray();
            }

            return keyDeleted;
        }

        private void SaveRecentSnapshotNodeIdAndHash(ulong depth, ulong totalBlocks)
        {
            ulong nodeIdSaved = 0, fromBlock = StartingBlockToKeep(depth, totalBlocks);
            Logger.LogTrace($"Saving nodeId and nodeHash for snapshots in range [{fromBlock}, {totalBlocks}]. All other "
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

        private void DeleteRecentSnapshotNodeIdAndHash(ulong depth, ulong totalBlocks)
        {
            Task.Factory.StartNew(() =>
            {
                DeleteSavedNodeId();
            }, TaskCreationOptions.LongRunning);

            Task.Factory.StartNew(() =>
            {
                DeleteSavedNodeHash();
            }, TaskCreationOptions.LongRunning);

            lock (_deletionWorker)
            {
                dbShrinkStatus = DbShrinkStatus.AsyncDeletionStarted;
                Monitor.Wait(_deletionWorker);
            }
        }

        private void DeleteSavedNodeId()
        {
            Logger.LogTrace($"Deleting saved nodeId");
            ulong nodeIdDeleted = 0;
            var prefix = EntryPrefix.NodeIdForRecentSnapshot.BuildPrefix();
            nodeIdDeleted = DeleteAllForPrefix(prefix);
            Logger.LogTrace($"Deleted {nodeIdDeleted} nodeId in total");
            NotifyCaller();
        }

        private void DeleteSavedNodeHash()
        {
            Logger.LogTrace($"Deleting saved nodeHash");
            ulong nodeHashDeleted = 0;
            var prefix = EntryPrefix.NodeHashForRecentSnapshot.BuildPrefix();
            nodeHashDeleted = DeleteAllForPrefix(prefix);
            Logger.LogTrace($"Deleted {nodeHashDeleted} nodeHash in total");
            NotifyCaller();
        }

        private ulong DeleteAllForPrefix(byte[] prefix)
        {
            ulong keyDeleted = 0;
            var iterator = _repository.GetIteratorForPrefixOnly(prefix);
            if (!(iterator is null))
            {
                while (iterator.Valid())
                {
                    keyDeleted++;
                    _repository.Delete(iterator.Key());
                    iterator.Next();
                }
            }
            return keyDeleted;
        }

        private void NotifyCaller()
        {
            var checkInterval = 1000; // 1 second
            // DbShrinkStatus.AsyncDeletionStarted means the process is requested
            // there are 2 parallel threads running
            // DbShrinkStatus.DeletionStep1Complete means one of the thread is complete
            // notify caller only if all threads are complete
            while (dbShrinkStatus != DbShrinkStatus.AsyncDeletionStarted 
                && dbShrinkStatus != DbShrinkStatus.DeletionStep1Complete)
            {
                Thread.Sleep(checkInterval);
            }
            lock (_deletionWorker)
            {
                if (dbShrinkStatus == DbShrinkStatus.DeletionStep1Complete)
                {
                    dbShrinkStatus = DbShrinkStatus.DeletionStep2Complete;
                }
                else dbShrinkStatus = DbShrinkStatus.DeletionStep1Complete;
                if (dbShrinkStatus == DbShrinkStatus.DeletionStep2Complete)
                {
                    Monitor.PulseAll(_deletionWorker);
                }
            }
        }

    }


}