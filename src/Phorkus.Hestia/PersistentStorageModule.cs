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
            containerBuilder.RegisterSingleton<ISnapshotManager<IBalanceSnapshot>, BalanceManager>();
            containerBuilder.RegisterSingleton<ISnapshotManager<IAssetSnapshot>, AssetManager>();
            containerBuilder.RegisterSingleton<ISnapshotManager<IStorageSnapshot>, StorageManager>();
            containerBuilder.RegisterSingleton<IPersistentStorageManager, PersistentStorageManager>();
            containerBuilder.RegisterSingleton<IBlockchainStateManager, BlockchainStateManager>();
        }
    }
}