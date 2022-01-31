using System.Collections.Generic;
using Lachain.Proto;

namespace Lachain.Core.Consensus
{
    public interface IConsensusManager
    {
        void Dispatch(ConsensusMessage message, ECDSAPublicKey publicKey);
        void Start(ulong startingEra);
        void Terminate();
        EraBroadcaster GetEras(long era);
    }
}