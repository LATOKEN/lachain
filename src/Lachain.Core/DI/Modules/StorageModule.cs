using Lachain.Core.Config;
using Lachain.Storage;
using Lachain.Storage.Repositories;
using Lachain.Storage.State;

namespace Lachain.Core.DI.Modules
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
            containerBuilder.RegisterSingleton<IPoolRepository, PoolRepository>();
            containerBuilder.RegisterSingleton<ISnapshotIndexRepository, SnapshotIndexRepository>();
            containerBuilder.RegisterSingleton<IKeyGenRepository, KeyGenRepository>();
            containerBuilder.RegisterSingleton<IValidatorAttendanceRepository, ValidatorAttendanceRepository>();
        }
    }
}