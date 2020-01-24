using System.Collections.Generic;
using Phorkus.Consensus;
using Phorkus.Consensus.Messages;
using Phorkus.Networking;
using Phorkus.Proto;

namespace Phorkus.Core.Consensus
{
    public class ConsensusBroadcaster : IConsensusBroadcaster
    {
        private readonly INetworkManager _networkManager;
        private readonly PublicKey[] _publicKeys;

        public ConsensusBroadcaster(INetworkManager networkManager, INetworkBroadcaster networkBroadcaster)
        {
            _networkManager = networkManager;
        }

        public void RegisterProtocols(IEnumerable<IConsensusProtocol> protocols)
        {
            throw new System.NotImplementedException();
        }

        public void Broadcast(ConsensusMessage message)
        {
            var payload = _networkManager.MessageFactory.ConsensusMessage(message);
            foreach (var publicKey in _publicKeys)
            {
                _networkManager.GetPeerByPublicKey(publicKey).Send(payload);
            }
        }

        public void SendToValidator(ConsensusMessage message, int index)
        {
            var payload = _networkManager.MessageFactory.ConsensusMessage(message);
            _networkManager.GetPeerByPublicKey(_publicKeys[index]).Send(payload);
        }

        public void Dispatch(ConsensusMessage message)
        {
            throw new System.NotImplementedException();
        }

        public void InternalRequest<TId, TInputType>(ProtocolRequest<TId, TInputType> request)
            where TId : IProtocolIdentifier
        {
            throw new System.NotImplementedException();
        }

        public void InternalResponse<TId, TResultType>(ProtocolResult<TId, TResultType> result)
            where TId : IProtocolIdentifier
        {
            throw new System.NotImplementedException();
        }

        public int GetMyId()
        {
            throw new System.NotImplementedException();
        }

        public Dictionary<IProtocolIdentifier, IConsensusProtocol> Registry { get; }

        public void Terminate()
        {
            throw new System.NotImplementedException();
        }
    }
}