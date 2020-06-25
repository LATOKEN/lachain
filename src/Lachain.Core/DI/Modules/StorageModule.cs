using System;
using System.IO;
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
            var dataDir = configManager.GetCliArg("datadir");
            dataDir ??= configManager.GetConfig<StorageConfig>("storage")!.Path!;
            dataDir = Path.IsPathRooted(dataDir) || dataDir.StartsWith("~/") ? 
                dataDir : 
                Path.GetDirectoryName(configManager.ConfigPath) + Path.DirectorySeparatorChar + dataDir;
            
            var dbContext = new RocksDbContext(dataDir);
            containerBuilder.Register<IRocksDbContext>(dbContext);

            /* repositories */
            containerBuilder.RegisterSingleton<IPoolRepository, PoolRepository>();
            containerBuilder.RegisterSingleton<ISnapshotIndexRepository, SnapshotIndexRepository>();
            containerBuilder.RegisterSingleton<IKeyGenRepository, KeyGenRepository>();
            containerBuilder.RegisterSingleton<IValidatorAttendanceRepository, ValidatorAttendanceRepository>();
            containerBuilder.RegisterSingleton<ILocalTransactionRepository, LocalTransactionRepository>();
        }
    }
}