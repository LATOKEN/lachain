using Phorkus.Core.Config;
using Phorkus.Storage;
using Phorkus.Storage.Repositories;
using Phorkus.Storage.RocksDB;
using Phorkus.Storage.RocksDB.Repositories;
using Phorkus.Storage.State;

namespace Phorkus.Core.DI.Modules
{
    public class StorageModule : IModule
    {
        public void Register(IContainerBuilder containerBuilder, IConfigManager configManager)
        {
            /* HMAT */
            containerBuilder.RegisterSingleton<ISnapshotManager<IBalanceSnapshot>, BalanceManager>();
            containerBuilder.RegisterSingleton<ISnapshotManager<IAssetSnapshot>, AssetManager>();
            containerBuilder.RegisterSingleton<IPersistentStorageManager, PersistentStorageManager>();
            containerBuilder.RegisterSingleton<IBlockchainStateManager, BlockchainStateManager>();
            /* global */
            containerBuilder.RegisterSingleton<IRocksDbContext, RocksDbContext>();
            /* repositories */
            containerBuilder.RegisterSingleton<IBlockRepository, BlockRepository>();
            containerBuilder.RegisterSingleton<IContractRepository, ContractRepository>();
            containerBuilder.RegisterSingleton<IGlobalRepository, GlobalRepository>();
            containerBuilder.RegisterSingleton<ITransactionRepository, TransactionRepository>();
            containerBuilder.RegisterSingleton<IWithdrawalRepository, WithdrawalRepository>();
        }
    }
}