using Phorkus.Core.Config;
using Phorkus.Core.Cryptography;
using Phorkus.Core.DI;

namespace Phorkus.Core
{
    public class CryptographyModule : IModule
    {
        public void Register(IContainerBuilder containerBuilder, IConfigManager configManager)
        {
            containerBuilder.RegisterSingleton<ICrypto, BouncyCastle>();
        }
    }
}