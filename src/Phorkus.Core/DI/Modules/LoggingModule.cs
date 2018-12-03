using Phorkus.Core.Config;
using Phorkus.Core.DI;

namespace Phorkus.Logger
{
    public class LoggingModule : IModule
    {
        public void Register(IContainerBuilder containerBuilder, IConfigManager configManager)
        {
            containerBuilder.RegisterSingleton<ILoggerFactoryExtended, NLogLoggerFactory>();
            containerBuilder.RegisterSingleton(typeof(ILogger<>), typeof(LoggerAdapter<>));
            containerBuilder.RegisterSingleton(typeof(ILoggerProvider<>), typeof(LoggerProvider<>));
        }
    }
}