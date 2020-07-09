using System;
using Lachain.Proto;

namespace Lachain.Networking
{
    public interface IRemotePeer
    {
        bool IsConnected { get; }
        PeerAddress Address { get; }
        ECDSAPublicKey? PublicKey { get; }
        DateTime Connected { get; }
        void Send(NetworkMessage message);
    }
}