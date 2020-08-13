using Lachain.Core.Config;
using Lachain.Core.Network;
using Lachain.Networking;

namespace Lachain.Core.DI.Modules
{
    public class NetworkModule : IModule
    {
        public void Register(IContainerBuilder containerBuilder, IConfigManager configManager)
        {
            containerBuilder.RegisterSingleton<INetworkManager, NetworkManager>();
            containerBuilder.RegisterSingleton<IPeerManager, PeerManager>();
            containerBuilder.RegisterSingleton<IBlockSynchronizer, BlockSynchronizer>();
            containerBuilder.RegisterSingleton<IMessageHandler, MessageHandler>();
        }
    }
}