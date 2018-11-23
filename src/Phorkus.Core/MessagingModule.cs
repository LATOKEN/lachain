using Phorkus.Core.Config;
using Phorkus.Core.DI;
using Phorkus.Core.Messaging;

namespace Phorkus.Core
{
    public class MessagingModule : IModule
    {
        public void Register(IContainerBuilder containerBuilder, IConfigManager configManager)
        {
            containerBuilder.RegisterSingleton<IMessagingManager, MessagingManager>();
            containerBuilder.RegisterSingleton<IMessageFactory, MessageFactory>();
            containerBuilder.RegisterSingleton<IBlockchainSynchronizer, BlockchainSynchronizer>();
        }
    }
}