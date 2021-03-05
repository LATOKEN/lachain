using Lachain.Core.BlockchainFilter;
using Lachain.Core.Config;
using Lachain.Core.RPC;
using Lachain.Core.RPC.HTTP;

namespace Lachain.Core.DI.Modules
{
    public class RpcModule : IModule
    {
        public void Register(IContainerBuilder containerBuilder, IConfigManager configManager)
        {
            containerBuilder.RegisterSingleton<IBlockchainEventFilter, BlockchainEventFilter>();
            containerBuilder.RegisterSingleton<IRpcManager, RpcManager>();
            containerBuilder.RegisterSingleton<IMetricsService, MetricsService>();
        }
    }
}