using Phorkus.Core.Config;
using Phorkus.Core.CrossChain;

namespace Phorkus.Core.DI.Modules
{
    public class CrossChainModule: IModule
    {
        public void Register(IContainerBuilder containerBuilder, IConfigManager configManager)
        {
            containerBuilder.RegisterSingleton<ICrossChain, CrossChain.CrossChain>();
        }
    }
}