using Lachain.Core.BlockchainFilter;
using Lachain.Core.RPC;
using Microsoft.Extensions.DependencyInjection;

namespace Lachain.Core.DI
{
    public static class RpcModule
    {
        public static IServiceCollection AddServices(IServiceCollection services)
        {
            return services
                .AddSingleton<IBlockchainEventFilter, BlockchainEventFilter>()
                .AddSingleton<IRpcManager, RpcManager>();
        }
    }
}