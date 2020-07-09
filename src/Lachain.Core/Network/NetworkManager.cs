using Lachain.Core.Config;
using Lachain.Core.Vault;
using Lachain.Networking;

namespace Lachain.Core.Network
{
    public class NetworkManager : NetworkManagerBase
    {
        public NetworkManager(
            IConfigManager configManager,
            IPrivateWallet privateWallet
        )
            : base(configManager.GetConfig<NetworkConfig>("network"), privateWallet.EcdsaKeyPair)
        {
        }
    }
}