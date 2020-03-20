using System;
using Lachain.Proto;

namespace Lachain.Networking
{
    public interface IRemotePeer
    {
        bool IsConnected { get; }

        bool IsKnown { get; set; }

        PeerAddress Address { get; }
        
        Node? Node { get; set; }
        
        DateTime Connected { get; }

        void Send(NetworkMessage message);
    }
}