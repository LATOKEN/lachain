using Phorkus.Core.Config;

namespace Phorkus.Core.DI
{
    public interface IModule
    {
        void Register(IContainerBuilder containerBuilder, IConfigManager configManager);
    }
}