using Phorkus.Core.Blockchain.State;
using Phorkus.Core.Config;
using Phorkus.Core.DI;
using Phorkus.Hestia.Repositories;
using Phorkus.Hestia.State;

namespace Phorkus.Hestia
{
    public class PersistentStorageModule : IModule
    {
        public void Register(IContainerBuilder containerBuilder, IConfigManager configManager)
        {
            containerBuilder.RegisterSingleton<IBalanceManager, BalanceManager>();
            containerBuilder.RegisterSingleton<IStorageManager, StorageManager>();
            containerBuilder.RegisterSingleton<IBlockchainStateManager, BlockchainStateManager>();
        }
    }
}