using Phorkus.Core.Config;
using Phorkus.Core.DI;
using Phorkus.Core.Network;

namespace Phorkus.Core
{
    public class NetworkModule : IModule
    {
        public void Register(IContainerBuilder containerBuilder, IConfigManager configManager)
        {
            containerBuilder.RegisterSingleton<INetworkManager, NetworkManager>();
            containerBuilder.RegisterSingleton<INetworkContext, NetworkContext>();
            containerBuilder.RegisterSingleton<IBroadcaster, Broadcaster>();
        }
    }
}