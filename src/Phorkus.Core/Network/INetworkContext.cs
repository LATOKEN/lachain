using System.Collections.Concurrent;
using Phorkus.Core.Proto;

namespace Phorkus.Core.Network
{
    public interface INetworkContext
    {
        Node LocalNode { get; }
        
        ConcurrentDictionary<IpEndPoint, IPeer> ActivePeers { get; }
    }
}