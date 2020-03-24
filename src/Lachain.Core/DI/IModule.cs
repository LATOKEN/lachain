using Lachain.Core.Config;

namespace Lachain.Core.DI
{
    public interface IModule
    {
        void Register(IContainerBuilder containerBuilder, IConfigManager configManager);
    }
}