using System.IO;
using Lachain.Core.Config;
using Lachain.Storage;
using Lachain.Storage.Repositories;
using Lachain.Storage.State;
using Microsoft.Extensions.DependencyInjection;

namespace Lachain.Core.DI
{
    public static class StorageModule
    {
        public static IServiceCollection AddServices(IServiceCollection services, IConfigManager configManager)
        {
            var dataDir = configManager.CommandLineOptions.DataDir;
            dataDir ??= configManager.GetConfig<StorageConfig>("storage")!.Path!;
            dataDir = Path.IsPathRooted(dataDir) || dataDir.StartsWith("~/")
                ? dataDir
                : Path.Join(Path.GetDirectoryName(Path.GetFullPath(configManager.ConfigPath)), dataDir);

            return services
                .AddSingleton<IStorageManager, StorageManager>()
                .AddSingleton<IStateManager, StateManager>()
                .AddSingleton<IRocksDbContext, RocksDbContext>(provider => new RocksDbContext(dataDir))
                .AddSingleton<IPoolRepository, PoolRepository>()
                .AddSingleton<ISnapshotIndexRepository, SnapshotIndexRepository>()
                .AddSingleton<IKeyGenRepository, KeyGenRepository>()
                .AddSingleton<IValidatorAttendanceRepository, ValidatorAttendanceRepository>()
                .AddSingleton<ILocalTransactionRepository, LocalTransactionRepository>();
        }
    }
}