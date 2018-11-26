using Phorkus.Core.Config;

namespace Phorkus.Core.DI.Modules
{
    public class ConfigModule : IModule
    {
        public void Register(IContainerBuilder containerBuilder, IConfigManager configManager)
        {
            containerBuilder.RegisterInstance(configManager);
        }
    }
}