using Phorkus.Core.Blockchain.State;
using Phorkus.Core.Config;
using Phorkus.Core.DI;
using Phorkus.Core.Storage;
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
            containerBuilder.RegisterSingleton<IBlockRepository, BlockRepository>();
            containerBuilder.RegisterSingleton<IContractRepository, ContractRepository>();
            containerBuilder.RegisterSingleton<IGlobalRepository, GlobalRepository>();
            containerBuilder.RegisterSingleton<ITransactionRepository, TransactionRepository>();
        }
    }
}