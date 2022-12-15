using System;
using Lachain.Networking.Hub;

namespace Lachain.Networking.PeerFault
{
    public interface IPeerBanManager
    {
        void BanPeer(byte[] publicKey);
        event EventHandler<(byte[] publicKey, ulong penalties)>? OnPeerBanned;
    }
}