using System;
using System.Collections.Generic;
using Phorkus.Consensus;
using Phorkus.Consensus.BinaryAgreement;
using Phorkus.Consensus.CommonCoin;
using Phorkus.Consensus.Messages;
using Phorkus.Consensus.ReliableBroadcast;
using Phorkus.Consensus.TPKE;
using Phorkus.Proto;

namespace Phorkus.ConsensusTest
{
    public class BroadcastSimulator : IConsensusBroadcaster
    {
        private readonly int _sender;

        private readonly Dictionary<IProtocolIdentifier, IConsensusProtocol> _registry =
            new Dictionary<IProtocolIdentifier, IConsensusProtocol>();

        private readonly Dictionary<IProtocolIdentifier, IProtocolIdentifier> _callback =
            new Dictionary<IProtocolIdentifier, IProtocolIdentifier>();

        private readonly PlayerSet _playerSet;

        public BroadcastSimulator(int sender, PlayerSet playerSet)
        {
            _sender = sender;
            _playerSet = playerSet;
            _playerSet.AddPlayer(this);
        }


        public void RegisterProtocols(IEnumerable<IConsensusProtocol> protocols)
        {
            foreach (var protocol in protocols)
            {
                if (_registry.ContainsKey(protocol.Id)) 
                    throw new InvalidOperationException($"Protocol with id ({protocol.Id}) already registered");
                _registry[protocol.Id] = protocol;
            }
        }

        public void Broadcast(ConsensusMessage message)
        {
            _playerSet.BroadcastMessage(message);
        }

        public void SendToValidator(ConsensusMessage message, int index)
        {
            _playerSet.SendToPlayer(message, index);
        }

        public void Dispatch(ConsensusMessage message)
        {
            switch (message.PayloadCase)
            {
                case ConsensusMessage.PayloadOneofCase.Bval:
                    _registry[new BinaryBroadcastId(message.Validator.Era, message.Bval.Agreement, message.Bval.Epoch)]
                        ?.ReceiveMessage(new MessageEnvelope(message));
                    break;
                case ConsensusMessage.PayloadOneofCase.Aux:
                    _registry[new BinaryBroadcastId(message.Validator.Era, message.Aux.Agreement, message.Aux.Epoch)]
                        ?.ReceiveMessage(new MessageEnvelope(message));
                    break;
                case ConsensusMessage.PayloadOneofCase.Conf:
                    _registry[new BinaryBroadcastId(message.Validator.Era, message.Conf.Agreement, message.Conf.Epoch)]
                        ?.ReceiveMessage(new MessageEnvelope(message));
                    break;
                case ConsensusMessage.PayloadOneofCase.Coin:
                    _registry[new CoinId(message.Validator.Era, message.Coin.Agreement, message.Coin.Epoch)]
                        ?.ReceiveMessage(new MessageEnvelope(message));
                    break;
                case ConsensusMessage.PayloadOneofCase.PrivateKey:
                    _registry[new TPKESetupId((int) message.Validator.Era)]?.ReceiveMessage(new MessageEnvelope(message));
                    break;
                default:
                    throw new InvalidOperationException($"Unknown message type {message.PayloadCase}");
            }
        }

        public void InternalRequest<TId, TInputType>(ProtocolRequest<TId, TInputType> request)
            where TId : IProtocolIdentifier
        {
            if (request.From != null)
            {
                if (_callback.ContainsKey(request.To))
                    throw new InvalidOperationException(
                        "Cannot have two requests from different protocols to one protocol");
                _callback[request.To] = request.From;
            }

            Console.Error.WriteLine($"Party {GetMyId()} received internal request from {request.From}");
//            if (!_registry.ContainsKey(request.To))
//            {
//                switch (request.To)
//                {
//                    case ReliableBroadcastId rbcId:
//                        new ReliableBroadcast();
//                        break;
//                }
//                RegisterProtocols(new []{});
//            }
            _registry[request.To]?.ReceiveMessage(new MessageEnvelope(request));
        }

        public void InternalResponse<TId, TResultType>(ProtocolResult<TId, TResultType> result)
            where TId : IProtocolIdentifier
        {
//            Console.Error.WriteLine($"Player {GetMyId()}: result from {result.From}");
            if (_callback.TryGetValue(result.From, out var senderId))
            {
                _registry[senderId]?.ReceiveMessage(new MessageEnvelope(result));
                Console.Error.WriteLine($"Player {GetMyId()} sent response to batya {senderId}");
            }

            // message is also delivered to self
            _registry[result.From]?.ReceiveMessage(new MessageEnvelope(result));
        }

        public int GetMyId()
        {
            return _sender;
        }
    }
}