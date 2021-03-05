using Lachain.Core.CLI;
using Microsoft.Extensions.DependencyInjection;

namespace Lachain.Core.DI
{
    public static class ConsoleModule
    {
        public static IServiceCollection AddServices(IServiceCollection services)
        {
            return services.AddSingleton<IConsoleManager, ConsoleManager>();
        }
    }
}