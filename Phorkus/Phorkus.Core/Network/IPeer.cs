using System;
using System.Collections.Generic;
using Phorkus.Core.Cryptography;
using Phorkus.Core.Network.Proto;
using Phorkus.Core.Proto;

namespace Phorkus.Core.Network
{
    public interface IPeer
    {
        event EventHandler<IPeer> OnDisconnect;
        
        bool IsConnected { get; }

        BloomFilter BloomFilter { get; set; }

        IpEndPoint EndPoint { get; }

        Node Node { get; set; }

        bool IsKnown { get; set; }
        
        DateTime Connected { get; }

        uint RateLimit { get; }

        void Send(Message message);

        IEnumerable<Message> Receive();

        void Disconnect();

        void Run();
    }
}