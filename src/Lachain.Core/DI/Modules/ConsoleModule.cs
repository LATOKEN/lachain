using Lachain.Core.CLI;
using Lachain.Core.Config;

namespace Lachain.Core.DI.Modules
{
    public class ConsoleModule : IModule
    {
        public void Register(IContainerBuilder containerBuilder, IConfigManager configManager)
        {
            containerBuilder.RegisterSingleton<IConsoleManager, ConsoleManager>();
        }
    }
}
