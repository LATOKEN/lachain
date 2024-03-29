﻿using System.Collections.Generic;
using Lachain.Consensus.Messages;
using Lachain.Proto;

namespace Lachain.Consensus
{
    public interface IConsensusBroadcaster
    {
        void RegisterProtocols(IEnumerable<IConsensusProtocol> protocols);

        /*
         * This method broadcast message to all consensus nodes (including self)
         */
        void Broadcast(ConsensusMessage message);

        void SendToValidator(ConsensusMessage message, int index);

        /*
         * This method is used when external consensus message is incoming
         */
        void Dispatch(ConsensusMessage message, int from);

        void InternalRequest<TId, TInputType>(
            ProtocolRequest<TId, TInputType> request) where TId : IProtocolIdentifier;

        void InternalResponse<TId, TResultType>(
            ProtocolResult<TId, TResultType> result) where TId : IProtocolIdentifier;

        /*
         * This method is used to get validator id
         */
        int GetMyId();

        ECDSAPublicKey? GetPublicKeyById(int id);

        IConsensusProtocol? GetProtocolById(IProtocolIdentifier id);

        void Terminate();
    }
}