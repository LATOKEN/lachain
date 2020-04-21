using Lachain.Core.Config;

namespace Lachain.Core.DI.Modules
{
    public class MessagingModule : IModule
    {
        public void Register(IContainerBuilder containerBuilder, IConfigManager configManager)
        {
//            containerBuilder.RegisterSingleton<IMessagingManager, MessagingManager>();
//            containerBuilder.RegisterSingleton<IMessageFactory, MessageFactory>();
//            containerBuilder.RegisterSingleton<IBlockchainSynchronizer, BlockchainSynchronizer>();
        }
    }
}