using Lachain.Core.Network;
using Lachain.Networking;
using Microsoft.Extensions.DependencyInjection;

namespace Lachain.Core.DI
{
    public static class NetworkModule
    {
        public static IServiceCollection AddServices(IServiceCollection services)
        {
            return services.AddSingleton<INetworkManager, NetworkManager>()
                .AddSingleton<IBlockSynchronizer, BlockSynchronizer>()
                .AddSingleton<INetworkBroadcaster, NetworkManager>()
                .AddSingleton<IMessageHandler, MessageHandler>();
        }
    }
}