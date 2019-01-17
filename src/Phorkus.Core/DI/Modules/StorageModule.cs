using Phorkus.Core.Config;
using Phorkus.Storage;
using Phorkus.Storage.Repositories;
using Phorkus.Storage.State;

namespace Phorkus.Core.DI.Modules
{
    public class StorageModule : IModule
    {
        public void Register(IContainerBuilder containerBuilder, IConfigManager configManager)
        {
            /* HMAT */
            containerBuilder.RegisterSingleton<IStorageManager, StorageManager>();
            containerBuilder.RegisterSingleton<IStateManager, StateManager>();
            /* global */
            containerBuilder.RegisterSingleton<IRocksDbContext, RocksDbContext>();
            /* repositories */
            containerBuilder.RegisterSingleton<IBlockRepository, BlockRepository>();
            containerBuilder.RegisterSingleton<IGlobalRepository, GlobalRepository>();
            containerBuilder.RegisterSingleton<ITransactionRepository, TransactionRepository>();
            containerBuilder.RegisterSingleton<IWithdrawalRepository, WithdrawalRepository>();
        }
    }
}