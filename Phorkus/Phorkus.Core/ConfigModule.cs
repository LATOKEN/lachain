using Phorkus.Core.Config;
using Phorkus.Core.DI;

namespace Phorkus.Core
{
    public class ConfigModule : IModule
    {
        public void Register(IContainerBuilder containerBuilder, IConfigManager configManager)
        {
            containerBuilder.RegisterInstance(configManager);
        }
    }
}