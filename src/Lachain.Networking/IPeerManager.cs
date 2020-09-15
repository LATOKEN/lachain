using System;
using System.Collections.Generic;
using Lachain.Proto;

namespace Lachain.Networking
{
    public interface IPeerManager : IDisposable
    {
        List<Peer> Start();
        void Stop();
        void UpdatePeerTimestamp(ECDSAPublicKey publicKey);
        void BroadcastGetPeersRequest();

        Peer[] GetPeersToBroadcast();
        IEnumerable<Peer> HandlePeersFromPeer(IEnumerable<Peer> peers);
    }
}