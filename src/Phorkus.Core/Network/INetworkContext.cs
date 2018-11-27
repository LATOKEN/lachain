using System.Collections.Concurrent;
using Phorkus.Proto;

namespace Phorkus.Core.Network
{
    public interface INetworkContext
    {
        ConcurrentDictionary<PeerAddress, IRemotePeer> ActivePeers { get; }
        
        Node LocalNode { get; }
    }
}