using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Lachain.Consensus.RequestProtocols.Messages;
using Lachain.Consensus.RequestProtocols.Messages.Requests;
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
        private readonly IDictionary<byte, IMessageRequestHandler> _messageHandlers;
        private bool _terminated = false;
        private bool _subscribed = false;
        public ProtocolRequestHandler(IProtocolIdentifier id, IConsensusProtocol protocol, int validatorsCount)
        {
            _validatorsCount = validatorsCount;
            _protocolId = id;
            _type = ProtocolUtils.GetProtocolType(id);
            _messageHandlers = new ConcurrentDictionary<byte, IMessageRequestHandler>();
            var requestTypes = Enum.GetValues(typeof(RequestType)).Cast<RequestType>().ToArray();
            foreach (var requestType in requestTypes)
            {
                if (ProtocolUtils.GetProtocolTypeForRequestType(requestType) == _type)
                {
                    if (_messageHandlers.TryGetValue((byte) requestType, out var handler))
                    {
                        throw new Exception($"{requestType} already registered with handler {handler.Type} and trying to register again");
                    }
                    _messageHandlers[(byte) requestType] = RegisterMessageHandler(requestType);
                }
            }
            protocol._receivedExternalMessage += MessageReceived;
        }

        public void Terminate()
        {
            if (_terminated)
                return;
            _terminated = true;
            foreach (var (_, handler) in _messageHandlers)
                handler.Terminate();
            _messageHandlers.Clear();
            Logger.LogTrace($"Protocol handler for protocol {_protocolId} terminated");
        }

        private void MessageReceived(object? sender, (int from, ConsensusMessage msg) @event)
        {
            if (_terminated)
                return;
            var (from, msg) = @event;
            var type = MessageUtils.GetRequestTypeForMessageType(msg);
            if (_messageHandlers.TryGetValue((byte) type, out var handler))
            {
                handler.MessageReceived(from, msg, type);
            }
            else throw new Exception($"MessageRequestHandler {type} not registered");
        }

        public List<(ConsensusMessage, int)> GetRequests(int requestCount)
        {
            var allRequests = new List<(ConsensusMessage, int)>();
            if (_terminated)
                return allRequests;
            foreach (var (_, handler) in _messageHandlers)
            {
                var requests = handler.GetRequests(_protocolId, requestCount);
                requestCount -= requests.Count;
                allRequests.AddRange(requests);
            }
            return allRequests;
        }

        private IMessageRequestHandler RegisterMessageHandler(RequestType type)
        {
            switch (type)
            {
                case RequestType.Aux:
                    return new AuxRequest(type, _validatorsCount, 1);
                case RequestType.Bval:
                    return new BValRequest(type, _validatorsCount, 2);
                case RequestType.Coin:
                    return new CoinRequest(type, _validatorsCount, 1);
                case RequestType.Conf:
                    return new ConfRequest(type, _validatorsCount, 1);
                case RequestType.Decrypted:
                    return new DecryptedRequest(type, _validatorsCount, _validatorsCount);
                case RequestType.Echo:
                    return new EchoRequest(type, _validatorsCount, 1);
                case RequestType.Val:
                    return new ValRequest(type, 1, 1);
                case RequestType.Ready:
                    return new ReadyRequest(type, _validatorsCount, 1);
                case RequestType.SignedHeader:
                    return new SignedHeaderRequest(type, _validatorsCount, 1);
                default:
                    throw new Exception($"RegisterMessageHandler Not implemented for request type {type}");
            }
        }
    }
}