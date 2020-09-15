using Lachain.Core.Config;
using Lachain.Core.Vault;
using Lachain.Networking;
using Lachain.Storage.Repositories;

namespace Lachain.Core.Network
{
    public class NetworkManager : NetworkManagerBase
    {
        public NetworkManager(
            IConfigManager configManager, IPrivateWallet privateWallet, IPeerRepository peerRepository
        )
            : base(configManager.GetConfig<NetworkConfig>("network")!, privateWallet.EcdsaKeyPair, peerRepository)
        {
        }
    }
}