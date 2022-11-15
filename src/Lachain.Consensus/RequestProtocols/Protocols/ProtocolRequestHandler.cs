using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Consensus.BinaryAgreement;
using Lachain.Consensus.CommonCoin;
using Lachain.Consensus.HoneyBadger;
using Lachain.Consensus.ReliableBroadcast;
using Lachain.Consensus.RequestProtocols.Messages;
using Lachain.Consensus.RootProtocol;
using Lachain.Logger;
using Lachain.Proto;

namespace Lachain.Consensus.RequestProtocols.Protocols
{
    public class ProtocolRequestHandler : IProtocolRequestHandler
    {
        private static readonly ILogger<ProtocolRequestHandler> Logger = LoggerFactory.GetLoggerForClass<ProtocolRequestHandler>();
        private readonly IProtocolIdentifier _protocolId;
        private readonly ProtocolType _type;
        private readonly int _validatorsCount;
        private readonly List<IMessageRequestHandler> _messageHandlers;
        private bool _terminated = false;
        public ProtocolRequestHandler(IProtocolIdentifier id, int validatorsCount)
        {
            _validatorsCount = validatorsCount;
            _protocolId = id;
            _type = ProtocolUtils.GetProtocolType(id);
            _messageHandlers = new List<IMessageRequestHandler>();
            var requestTypes = Enum.GetValues(typeof(RequestType)).Cast<RequestType>().ToArray();
            foreach (var type in requestTypes)
            {
                if (ProtocolUtils.GetProtocolTypeForRequestType(type) == _type)
                {
                    _messageHandlers.Add(RegisterMessageHandler(type));
                }
            }
        }

        public void Terminate()
        {
            if (_terminated)
                return;
            _terminated = true;
            foreach (var handler in _messageHandlers)
                handler.Terminate();
            _messageHandlers.Clear();
            Logger.LogTrace($"Protocol handler for protocol {_protocolId} terminated");
        }

        public void MessageReceived(int from, ConsensusMessage msg)
        {
            if (_terminated)
                return;
            var type = MessageUtils.GetRequestTypeForMessageType(msg);
            foreach (var handler in _messageHandlers)
            {
                if (handler.Type == type)
                {
                    handler.MessageReceived(from, msg, type);
                }
            }
        }

        public List<(ConsensusMessage, int)> GetRequests(int requestCount)
        {
            var allRequests = new List<(ConsensusMessage, int)>();
            if (_terminated)
                return allRequests;
            foreach (var handler in _messageHandlers)
            {
                var requests = handler.GetRequests(_protocolId, requestCount);
                requestCount -= requests.Count;
                allRequests.AddRange(requests);
            }
            return allRequests;
        }

        private IMessageRequestHandler RegisterMessageHandler(RequestType type)
        {
            int validators, msgPerValidator;
            switch (type)
            {
                case RequestType.Aux:
                    validators = _validatorsCount;
                    msgPerValidator = 1;
                    break;
                case RequestType.Bval:
                    validators = _validatorsCount;
                    msgPerValidator = 2;
                    break;
                case RequestType.Coin:
                    validators = _validatorsCount;
                    msgPerValidator = 1;
                    break;
                case RequestType.Conf:
                    validators = _validatorsCount;
                    msgPerValidator = 1;
                    break;
                case RequestType.Decrypted:
                    validators = _validatorsCount;
                    msgPerValidator = _validatorsCount;
                    break;
                case RequestType.Echo:
                    validators = _validatorsCount;
                    msgPerValidator = 1;
                    break;
                case RequestType.Val:
                    validators = 1;
                    msgPerValidator = 1;
                    break;
                case RequestType.Ready:
                    validators = _validatorsCount;
                    msgPerValidator = 1;
                    break;
                case RequestType.SignedHeader:
                    validators = _validatorsCount;
                    msgPerValidator = 1;
                    break;
                default:
                    throw new Exception($"RegisterMessageHandler Not implemented for request type {type}");
            }

            return new MessageRequestHandler(type, validators, msgPerValidator);
        }
    }
}