using System;
using Phorkus.Core.Cryptography;
using Phorkus.Core.Network.Proto;
using Phorkus.Core.Proto;

namespace Phorkus.Core.Network
{
    public interface IPeer
    {
        event EventHandler OnDisconnect;
        
        bool IsConnected { get; }

        BloomFilter BloomFilter { get; set; }

        IpEndPoint EndPoint { get; }

        Node Node { get; set; }

        bool IsReady { get; set; }

        DateTime Connected { get; }
        
        void Send(Message message);

        Message Receive();

        void Disconnect();

        void Run();
    }
}