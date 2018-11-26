using Phorkus.Core.Config;
using Phorkus.Core.Cryptography;

namespace Phorkus.Core.DI.Modules
{
    public class CryptographyModule : IModule
    {
        public void Register(IContainerBuilder containerBuilder, IConfigManager configManager)
        {
            containerBuilder.RegisterSingleton<ICrypto, BouncyCastle>();
        }
    }
}