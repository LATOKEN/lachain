using Lachain.Core.Blockchain.Validators;
using Lachain.Core.Config;
using Lachain.Core.Vault;
using Lachain.Networking;

namespace Lachain.Core.Network
{
    public class NetworkManager : NetworkManagerBase
    {
        public NetworkManager(
            IConfigManager configManager,
            IPrivateWallet privateWallet,
            IPeerManager peerManager
        )
            : base(configManager.GetConfig<NetworkConfig>("network"), privateWallet.EcdsaKeyPair, peerManager)
        {
        }
    }
}