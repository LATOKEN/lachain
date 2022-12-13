using Lachain.Networking.Hub;

namespace Lachain.Networking.PeerFault
{
    public interface IPeerBanManager
    {
        void BanPeer(byte[] publicKey);
    }
}