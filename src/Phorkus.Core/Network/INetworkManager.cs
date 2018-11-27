using System;

namespace Phorkus.Core.Network
{
    public interface INetworkManager
    {
        event EventHandler<IRemotePeer> OnPeerConnected;
        event EventHandler<IRemotePeer> OnPeerClosed;

        void Start();

        void Stop();
    }
}