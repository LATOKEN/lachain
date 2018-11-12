using NeoSharp.Core.DI;
using NeoSharp.Core.Exceptions;
using NeoSharp.Core.Storage;
using NeoSharp.Core.Storage.Blockchain;
using NeoSharp.RocksDB;
using NeoSharp.RocksDB.Repositories;

namespace NeoSharp.Application.DI
{
    public class PersistenceModule : IModule
    {
        public void Register(IContainerBuilder containerBuilder)
        {
            containerBuilder.RegisterSingleton<PersistenceConfig>();

            var cfg = PersistenceConfig.Instance();

            switch (cfg.Provider)
            {
                /*case RedisDbJsonConfig.Provider:
                    {
                        containerBuilder.RegisterSingleton<RedisDbJsonConfig>();
                        containerBuilder.RegisterSingleton<IRepository, RedisDbJsonRepository>();
                        containerBuilder.RegisterSingleton<IRedisDbJsonContext, RedisDbJsonContext>();
                        break;
                    }

                case RedisDbBinaryConfig.Provider:
                    {
                        containerBuilder.RegisterSingleton<RedisDbBinaryConfig>();
                        containerBuilder.RegisterSingleton<IRepository, RedisDbBinaryRepository>();
                        containerBuilder.RegisterSingleton<IRedisDbContext, RedisDbContext>();
                        break;
                    }*/

                case RocksDbConfig.Provider:
                    {
                        containerBuilder.RegisterSingleton<RocksDbConfig>();
                        containerBuilder.RegisterSingleton<IRocksDbContext, RocksDbContext>();
                        /* register repositories */
                        containerBuilder.RegisterSingleton<IAssetRepository, AssetRepository>();
                        containerBuilder.RegisterSingleton<IBlockRepository, BlockRepository>();
                        containerBuilder.RegisterSingleton<IContractRepository, ContractRepository>();
                        containerBuilder.RegisterSingleton<IGlobalRepository, GlobalRepository>();
                        containerBuilder.RegisterSingleton<IStorageRepository, StorageRepository>();
                        containerBuilder.RegisterSingleton<ITransactionRepository, TransactionRepository>();
                        break;
                    }

                default:
                    throw new UnknownPersistentProvider($"The persistence configuration contains unknown provider \"{cfg.Provider}\"");
            }
        }
    }
}