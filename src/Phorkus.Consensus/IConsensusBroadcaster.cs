using Phorkus.Consensus.Messages;
using Phorkus.Proto;

namespace Phorkus.Consensus
{
    public interface IConsensusBroadcaster
    {
        /*
         * This method broadcast message to all consensus nodes (including self)
         */
        void Broadcast(ConsensusMessage message);
        
        /*
         * This method is used to send internal messages to self
         */
        void MessageSelf(InternalMessage message);
    }
}