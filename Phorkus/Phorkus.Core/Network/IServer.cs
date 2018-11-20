using System;

namespace Phorkus.Core.Network
{
    public interface IServer
    {
        bool IsWorking { get; }

        event EventHandler<IPeer> OnPeerConnected;
        event EventHandler<IPeer> OnPeerClosed;
        event EventHandler<IPeer> OnPeerAccepted;
        
        void Start();
        void Stop();
        
        IPeer ConnectTo(IpEndPoint ipEndPoint);
    }
}