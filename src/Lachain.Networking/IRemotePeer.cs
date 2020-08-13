using System;
using Lachain.Proto;

namespace Lachain.Networking
{
    public interface IRemotePeer
    {
        bool IsConnected { get; }
        PeerAddress Address { get; }
        ECDSAPublicKey? PeerPublicKey { get; }
        DateTime Connected { get; }
        void Send(NetworkMessage message);
        void ReceiveAck(ulong ackMessageId);
    }
}