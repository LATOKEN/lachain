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
        public ProtocolRequestHandler(IProtocolIdentifier id, int validatorsCount)
        {
            _validatorsCount = validatorsCount;
            _protocolId = id;
            _type = GetMyType(id);
            _messageHandlers = new List<IMessageRequestHandler>();
            var requestTypes = Enum.GetValues(typeof(RequestType)).Cast<RequestType>().ToArray();
            foreach (var type in requestTypes)
            {
                if (GetProtocolTypeForRequestType(type) == _type)
                {
                    _messageHandlers.Add(RegisterMessageHandler(type));
                }
            }
        }

        public void Terminate()
        {
            foreach (var handler in _messageHandlers)
                handler.Terminate();
            _messageHandlers.Clear();
            Logger.LogTrace($"Protocol handler for protocol {_protocolId} terminated");
        }

        public void MessageReceived(int from, ConsensusMessage msg)
        {
            var type = MessageRequestHandler.GetRequestTypeForMessageType(msg);
            foreach (var handler in _messageHandlers)
            {
                if (handler.Type == type)
                {
                    handler.MessageReceived(from, msg);
                }
            }
        }

        public List<(ConsensusMessage, int)> GetRequests(int requestCount)
        {
            var allRequests = new List<(ConsensusMessage, int)>();
            foreach (var handler in _messageHandlers)
            {
                var requests = handler.GetRequests(_protocolId, requestCount);
                requestCount -= requests.Count;
                allRequests.AddRange(requests);
            }
            return allRequests;
        }

        private ProtocolType GetMyType(IProtocolIdentifier id)
        {
            switch (id)
            {
                case RootProtocolId _:
                    return ProtocolType.Root;
                case HoneyBadgerId _:
                    return ProtocolType.HoneyBadger;
                case ReliableBroadcastId _:
                    return ProtocolType.ReliableBroadcast;
                case BinaryBroadcastId _:
                    return ProtocolType.BinaryBroadcast;
                case CoinId _:
                    return ProtocolType.CommonCoin;
                default:
                    throw new Exception($"Not implemented type for protocol id {id}");
            }
        }

        private ProtocolType GetProtocolTypeForRequestType(RequestType requestType)
        {
            switch (requestType)
            {
                case RequestType.Aux:
                    return ProtocolType.BinaryBroadcast;
                case RequestType.Bval:
                    return ProtocolType.BinaryBroadcast;
                case RequestType.Coin:
                    return ProtocolType.CommonCoin;
                case RequestType.Conf:
                    return ProtocolType.BinaryBroadcast;
                case RequestType.Decrypted:
                    return ProtocolType.HoneyBadger;
                case RequestType.Echo:
                    return ProtocolType.ReliableBroadcast;
                case RequestType.Ready:
                    return ProtocolType.ReliableBroadcast;
                case RequestType.SignedHeader:
                    return ProtocolType.Root;
                case RequestType.Val:
                    return ProtocolType.ReliableBroadcast;
                default:
                    throw new Exception($"No protocol type for request type {requestType}");
            }
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