using Lachain.Core.Config;
using Lachain.Core.Network;
using Lachain.Networking;
using Lachain.Networking.PeerFault;

namespace Lachain.Core.DI.Modules
{
    public class NetworkModule : IModule
    {
        public void Register(IContainerBuilder containerBuilder, IConfigManager configManager)
        {
            containerBuilder.RegisterSingleton<INetworkManager, NetworkManager>();
            containerBuilder.RegisterSingleton<IBlockSynchronizer, BlockSynchronizer>();
            containerBuilder.RegisterSingleton<IMessageHandler, MessageHandler>();
            containerBuilder.RegisterSingleton<IPeerBanManager, PeerBanManager>();
            containerBuilder.RegisterSingleton<IBannedPeerTracker, BannedPeerTracker>();
        }
    }
}