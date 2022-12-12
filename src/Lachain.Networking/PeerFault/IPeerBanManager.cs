using Lachain.Networking.Hub;

namespace Lachain.Networking.PeerFault
{
    public interface IPeerBanManager
    {
        void AdvanceEra(ulong era);
        void RegisterPeer(ClientWorker peer);
        void BanPeer(byte[] publicKey);
    }
}