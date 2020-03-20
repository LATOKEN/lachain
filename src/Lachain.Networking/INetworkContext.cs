using System.Collections.Generic;
using Lachain.Proto;

namespace Lachain.Networking
{
    public interface INetworkContext
    {
        IDictionary<PeerAddress, IRemotePeer> ActivePeers { get; }
        
        Node? LocalNode { get; }

        IRemotePeer? GetPeerByPublicKey(ECDSAPublicKey publicKey);
    }
    
}