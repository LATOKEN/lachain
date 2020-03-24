using Lachain.Core.Config;
using Lachain.Crypto;

namespace Lachain.Core.DI.Modules
{
    public class CryptographyModule : IModule
    {
        public void Register(IContainerBuilder containerBuilder, IConfigManager configManager)
        {
            containerBuilder.RegisterSingleton<ICrypto, DefaultCrypto>();
        }
    }
}