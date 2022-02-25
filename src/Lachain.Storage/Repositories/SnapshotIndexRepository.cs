using System.Linq;
using System.Runtime.CompilerServices;
using Lachain.Logger;
using Lachain.Storage.State;
using Lachain.Utility.Serialization;

namespace Lachain.Storage.Repositories
{
    public class SnapshotIndexRepository : ISnapshotIndexRepository
    {
        private readonly IRocksDbContext _dbContext;
        private readonly IStorageManager _storageManager;

        private static readonly ILogger<SnapshotIndexRepository> Logger =
            LoggerFactory.GetLoggerForClass<SnapshotIndexRepository>();

        public SnapshotIndexRepository(IRocksDbContext dbContext, IStorageManager storageManager)
        {
            _dbContext = dbContext;
            _storageManager = storageManager;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IBlockchainSnapshot GetSnapshotForBlock(ulong block)
        {
            return new BlockchainSnapshot(
                new BalanceSnapshot(_storageManager.GetState(
                    (uint) RepositoryType.BalanceRepository,
                    GetVersion((uint) RepositoryType.BalanceRepository, block))
                ),
                new ContractSnapshot(_storageManager.GetState(
                    (uint) RepositoryType.ContractRepository,
                    GetVersion((uint) RepositoryType.ContractRepository, block)
                )),
                new StorageSnapshot(_storageManager.GetState(
                    (uint) RepositoryType.StorageRepository,
                    GetVersion((uint) RepositoryType.StorageRepository, block))
                ),
                new TransactionSnapshot(_storageManager.GetState(
                    (uint) RepositoryType.TransactionRepository,
                    GetVersion((uint) RepositoryType.TransactionRepository, block))
                ),
                new BlockSnapshot(_storageManager.GetState(
                    (uint) RepositoryType.BlockRepository,
                    GetVersion((uint) RepositoryType.BlockRepository, block))
                ),
                new EventSnapshot(_storageManager.GetState(
                    (uint) RepositoryType.EventRepository,
                    GetVersion((uint) RepositoryType.EventRepository, block))
                ),
                new ValidatorSnapshot(_storageManager.GetState(
                    (uint) RepositoryType.ValidatorRepository,
                    GetVersion((uint) RepositoryType.ValidatorRepository, block))
                )
            );
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void SaveSnapshotForBlock(ulong block, IBlockchainSnapshot snapshot)
        {
            SetVersion((uint) RepositoryType.BalanceRepository, block, snapshot.Balances.Version);
            SetVersion((uint) RepositoryType.ContractRepository, block, snapshot.Contracts.Version);
            SetVersion((uint) RepositoryType.StorageRepository, block, snapshot.Storage.Version);
            SetVersion((uint) RepositoryType.TransactionRepository, block, snapshot.Transactions.Version);
            SetVersion((uint) RepositoryType.BlockRepository, block, snapshot.Blocks.Version);
            SetVersion((uint) RepositoryType.EventRepository, block, snapshot.Events.Version);
            SetVersion((uint) RepositoryType.ValidatorRepository, block, snapshot.Validators.Version);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private ulong GetVersion(uint repository, ulong block)
        {
            var rawVersion = _dbContext.Get(EntryPrefix.SnapshotIndex.BuildPrefix(
                repository.ToBytes().Concat(block.ToBytes()).ToArray()
            ));
            if (rawVersion is null) throw new System.Exception($"snapshot for block: {block} is not found");
            return rawVersion.AsReadOnlySpan().ToUInt64();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void SetVersion(uint repository, ulong block, ulong version)
        {
            Logger.LogTrace($"Saved version {version} for {(RepositoryType) repository}");
            _dbContext.Save(
                EntryPrefix.SnapshotIndex.BuildPrefix(
                    repository.ToBytes().Concat(block.ToBytes()).ToArray()
                ),
                version.ToBytes()
            );
        }

        private void DeleteVersion(uint repository, ulong block, ulong version, RocksDbAtomicWrite batch)
        {
            System.Console.WriteLine($"Deleting version {version} for {(RepositoryType) repository}");
            batch.Delete(
                EntryPrefix.SnapshotIndex.BuildPrefix(
                    repository.ToBytes().Concat(block.ToBytes()).ToArray()));
        }

        public void DeleteOldSnapshot(ulong depth, ulong totalBlocks)
        {
            System.Console.WriteLine($"Keeping latest {depth+1} snapshots from block: {totalBlocks - depth} to {totalBlocks}");

            // saving nodes for recent (depth + 1) snapshots temporarily
            // so that all remaining nodes can be deleted
            ulong usefulNodes = 0;
            for(var block = totalBlocks - depth; block <= totalBlocks; block++)
            {
                var blockchainSnapshot = GetSnapshotForBlock(block);
                var snapshots = blockchainSnapshot.GetAllSnapshot();
                foreach(var snapshot in snapshots)
                {
                    var batch = new RocksDbAtomicWrite(_dbContext);
                    usefulNodes += snapshot.UpdateNodeIdToBatch(true, batch);
                    batch.Commit();
                }
            }

            // deleting all nodes that are not reachable from recent (depth+1) snapshots
            ulong deletedNodes = 0;
            for(ulong block = 0 ; block < totalBlocks - depth; block++)
            {
                var blockchainSnapshot = GetSnapshotForBlock(block);
                var snapshots = blockchainSnapshot.GetAllSnapshot();
                foreach(var snapshot in snapshots)
                {
                    var batch = new RocksDbAtomicWrite(_dbContext);
                    deletedNodes += snapshot.DeleteSnapshot(block, batch);
                    DeleteVersion(snapshot.RepositoryId, block, snapshot.Version, batch);
                    batch.Commit();
                }
            }

            // delete temporary nodes
            for(var block = totalBlocks - depth; block <= totalBlocks; block++)
            {
                var blockchainSnapshot = GetSnapshotForBlock(block);
                var snapshots = blockchainSnapshot.GetAllSnapshot();
                foreach(var snapshot in snapshots)
                {
                    var batch = new RocksDbAtomicWrite(_dbContext);
                    usefulNodes -= snapshot.UpdateNodeIdToBatch(false, batch);
                    batch.Commit();
                }
            }
            // If this happens that means there is bug in implementation
            if(usefulNodes != 0) throw new System.Exception("Bug in implementation");
        }
    }
}