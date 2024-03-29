using Lachain.Core.Config;
using Lachain.Core.Vault;
using Lachain.Networking;
using Lachain.Storage.Repositories;

namespace Lachain.Core.Network
{
    public class NetworkManager : NetworkManagerBase
    {
        public const int MyVersion = 14;
        public const int MinCompatiblePeerVersion = 14;
        
        public NetworkManager(
            IConfigManager configManager, IPrivateWallet privateWallet
        )
            : base(
                configManager.GetConfig<NetworkConfig>("network")!,
                privateWallet.EcdsaKeyPair,
                privateWallet.HubPrivateKey, 
                MyVersion, MinCompatiblePeerVersion
            )
        {
        }
    }
}