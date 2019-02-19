using Phorkus.Proto;

namespace Phorkus.Consensus
{
    public interface IConsensusBroadcaster
    {
        void Broadcast(ConsensusMessage message);
    }
}