using Lachain.Core.Config;
using System.IO;
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
            var dataDir = configManager.CommandLineOptions.DataDir;
            dataDir ??= configManager.GetConfig<StorageConfig>("storage")!.Path!;
            dataDir = Path.IsPathRooted(dataDir) || dataDir.StartsWith("~/")
                ? dataDir
                : Path.Join(Path.GetDirectoryName(Path.GetFullPath(configManager.ConfigPath)), dataDir);

            containerBuilder.RegisterSingleton<IRocksDbContext>(() => new RocksDbContext(dataDir));

            /* repositories */
            containerBuilder.RegisterSingleton<IPoolRepository, PoolRepository>();
            containerBuilder.RegisterSingleton<ISnapshotIndexRepository, SnapshotIndexRepository>();
            containerBuilder.RegisterSingleton<IKeyGenRepository, KeyGenRepository>();
            containerBuilder.RegisterSingleton<IValidatorAttendanceRepository, ValidatorAttendanceRepository>();
            containerBuilder.RegisterSingleton<ILocalTransactionRepository, LocalTransactionRepository>();
        }
    }
}