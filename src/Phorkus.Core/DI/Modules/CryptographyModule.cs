using Phorkus.Core.Config;
using Phorkus.Crypto;

namespace Phorkus.Core.DI.Modules
{
    public class CryptographyModule : IModule
    {
        public void Register(IContainerBuilder containerBuilder, IConfigManager configManager)
        {
            containerBuilder.RegisterSingleton<ICrypto, DefaultCrypto>();
        }
    }
}