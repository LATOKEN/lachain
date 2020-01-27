using System.Collections.Generic;
using Phorkus.Proto;

namespace Phorkus.Networking
{
    public interface INetworkContext
    {
        IDictionary<PeerAddress, IRemotePeer> ActivePeers { get; }
        
        Node LocalNode { get; }

        IRemotePeer GetPeerByPublicKey(ECDSAPublicKey publicKey);
    }
    
}