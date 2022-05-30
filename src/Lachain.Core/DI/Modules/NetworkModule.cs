using Lachain.Core.Config;
using Lachain.Core.Network;
using Lachain.Core.Network.FastSynchronizerBatch;
using Lachain.Networking;

namespace Lachain.Core.DI.Modules
{
    public class NetworkModule : IModule
    {
        public void Register(IContainerBuilder containerBuilder, IConfigManager configManager)
        {
            containerBuilder.RegisterSingleton<INetworkManager, NetworkManager>();
            containerBuilder.RegisterSingleton<IBlockSynchronizer, BlockSynchronizer>();
            containerBuilder.RegisterSingleton<IMessageHandler, MessageHandler>();

            /* fastsync */
            containerBuilder.RegisterSingleton<IRequestManager, RequestManager>();
            containerBuilder.RegisterSingleton<IBlockRequestManager, BlockRequestManager>();
            containerBuilder.RegisterSingleton<IDownloader, Downloader>();
            containerBuilder.RegisterSingleton<IFastSynchronizerBatch, FastSynchronizerBatch>();
        }
    }
}