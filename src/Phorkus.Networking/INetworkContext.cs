using System.Collections.Concurrent;
using Phorkus.Proto;

namespace Phorkus.Networking
{
    public interface INetworkContext
    {
        ConcurrentDictionary<PeerAddress, IRemotePeer> ActivePeers { get; }
        
        Node LocalNode { get; }

        IRemotePeer GetPeerByPublicKey(PublicKey publicKey);
    }
}