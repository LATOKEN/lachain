using Phorkus.Core.Config;
using Phorkus.Core.Network;
using Phorkus.Networking;

namespace Phorkus.Core.DI.Modules
{
    public class NetworkModule : IModule
    {
        public void Register(IContainerBuilder containerBuilder, IConfigManager configManager)
        {
            containerBuilder.RegisterSingleton<INetworkManager, NetworkManager>();
            containerBuilder.RegisterSingleton<IBlockSynchronizer, BlockSynchronizer>();
            containerBuilder.RegisterSingleton<IMessageHandler, MessageHandler>();
        }
    }
}