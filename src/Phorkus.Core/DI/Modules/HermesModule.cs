using Phorkus.Core.Config;
using Phorkus.Core.Threshold;

namespace Phorkus.Core.DI.Modules
{
    public class HermesModule : IModule
    {
        public void Register(IContainerBuilder containerBuilder, IConfigManager configManager)
        {
            containerBuilder.RegisterSingleton<IThresholdManager, ThresholdManager>();
        }
    }
}