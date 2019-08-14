using System.Collections.Generic;
using Phorkus.Consensus.Messages;
using Phorkus.Proto;

namespace Phorkus.Consensus
{
    public interface IConsensusBroadcaster
    {
        void RegisterProtocols(IEnumerable<IConsensusProtocol> protocols);

        /*
         * This method broadcast message to all consensus nodes (including self)
         */
        void Broadcast(ConsensusMessage message);

        /*
         * This method is used when external consensus message is incoming
         */
        void Dispatch(ConsensusMessage message);

        void InternalRequest<TId, TInputType>(
            ProtocolRequest<TId, TInputType> request) where TId : IProtocolIdentifier;

        void InternalResponse<TId, TResultType>(
            ProtocolResult<TId, TResultType> result) where TId : IProtocolIdentifier;

        /*
         * This method is used to get validator id
         */
        int GetMyId();
    }
}