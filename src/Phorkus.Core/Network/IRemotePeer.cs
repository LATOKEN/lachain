using System;
using Phorkus.Proto;

namespace Phorkus.Core.Network
{
    public interface IRemotePeer
    {
        bool IsConnected { get; }

        bool IsKnown { get; set; }

        PeerAddress Address { get; }
        
        Node Node { get; set; }

        IRateLimiter RateLimiter { get; }
        
        DateTime Connected { get; }

        IBlockchainService BlockchainService { get; }

        IConsensusService ConsensusService { get; }
    }
}