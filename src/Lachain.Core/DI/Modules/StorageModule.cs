using Lachain.Core.Config;
using System.IO;
using Lachain.Core.Network.FastSync;
using Lachain.Storage;
using Lachain.Storage.Repositories;
using Lachain.Storage.State;
using Lachain.Storage.Trie;
using Lachain.Storage.DbCompact;

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
            containerBuilder.RegisterSingleton<ICheckpointRepository, CheckpointRepository>();

            /* fastsync repo */
            containerBuilder.RegisterSingleton<IFastSyncRepository, FastSyncRepository>();
            containerBuilder.RegisterSingleton<IHybridQueue, HybridQueue>();

            /* database query */ 
            containerBuilder.RegisterSingleton<INodeRetrieval, NodeRetrieval>();

            /* Db Compact */
            containerBuilder.RegisterSingleton<IDbShrinkRepository, DbShrinkRepository>();
            containerBuilder.RegisterSingleton<IDbShrink, DbShrink>();
        }
    }
}