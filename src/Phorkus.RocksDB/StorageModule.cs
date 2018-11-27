using Phorkus.Core.Config;
using Phorkus.Core.DI;
using Phorkus.Core.Storage;
using Phorkus.Core.Storage.Repositories;
using Phorkus.RocksDB.Repositories;

namespace Phorkus.RocksDB
{
    public class StorageModule : IModule
    {
        public void Register(IContainerBuilder containerBuilder, IConfigManager configManager)
        {
            /* global */
            containerBuilder.RegisterSingleton<IRocksDbContext, RocksDbContext>();
            /* repositories */
            containerBuilder.RegisterSingleton<IAccountRepository, AccountRepository>();
            containerBuilder.RegisterSingleton<IAssetRepository, AssetRepository>();
            containerBuilder.RegisterSingleton<IBalanceRepository, BalanceRepository>();
            containerBuilder.RegisterSingleton<IBlockRepository, BlockRepository>();
            containerBuilder.RegisterSingleton<IContractRepository, ContractRepository>();
            containerBuilder.RegisterSingleton<IGlobalRepository, GlobalRepository>();
            containerBuilder.RegisterSingleton<IStorageRepository, StorageRepository>();
            containerBuilder.RegisterSingleton<ITransactionRepository, TransactionRepository>();
        }
    }
}