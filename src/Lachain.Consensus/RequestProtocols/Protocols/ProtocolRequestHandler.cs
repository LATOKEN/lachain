using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Linq;
using Lachain.Consensus.RequestProtocols.Messages;
using Lachain.Consensus.RequestProtocols.Messages.Requests;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Consensus.RequestProtocols.Protocols
{
    public class ProtocolRequestHandler : IProtocolRequestHandler
    {
        private static readonly ILogger<ProtocolRequestHandler> Logger = LoggerFactory.GetLoggerForClass<ProtocolRequestHandler>();
        private readonly IProtocolIdentifier _protocolId;
        private readonly ProtocolType _type;
        private readonly int _validatorsCount;
        private readonly IDictionary<byte, IMessageRequestHandler> _messageHandlers;
        private readonly List<RequestType> _orderedRequestTypes;
        private bool _terminated = false;
        private bool _subscribed = false;
        public ProtocolRequestHandler(IProtocolIdentifier id, IConsensusProtocol protocol, int validatorsCount)
        {
            _validatorsCount = validatorsCount;
            _protocolId = id;
            _type = ProtocolUtils.GetProtocolType(id);
            _messageHandlers = new ConcurrentDictionary<byte, IMessageRequestHandler>();
            var requestTypes = Enum.GetValues(typeof(RequestType)).Cast<RequestType>().ToArray();
            _orderedRequestTypes = new List<RequestType>();
            foreach (var requestType in requestTypes)
            {
                if (ProtocolUtils.GetProtocolTypeForRequestType(requestType) == _type)
                {
                    if (_messageHandlers.TryGetValue((byte) requestType, out var handler))
                    {
                        throw new Exception($"{requestType} already registered with handler {handler.Type} and trying to register again");
                    }
                    _messageHandlers[(byte) requestType] = RegisterMessageHandler(requestType);
                    _orderedRequestTypes.Add(requestType);
                }
            }
            _orderedRequestTypes = _orderedRequestTypes.OrderBy(type => type).ToList();
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
            var handler = GetMessageHandler(type);
            handler.MessageReceived(from, msg, type);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public List<(ConsensusMessage, int)> GetRequests(int requestCount)
        {
            var allRequests = new List<(ConsensusMessage, int)>();
            if (_terminated)
                return allRequests;

            // Tuple<validatorId, msgId, requestType>
            var processedRequest = new List<Tuple<int, int, RequestType>>();
            // getting requests that are not requested yet
            foreach (var requestType in _orderedRequestTypes)
            {
                var handler = GetMessageHandler(requestType);
                while (requestCount > 0)
                {
                    var peekRequest = handler.Peek();
                    if (peekRequest is null) break;
                    var (validatorId, msgId, _, status) = peekRequest;
                    if (status != MessageStatus.NotReceived) break;
                    processedRequest.Add(Tuple.Create(validatorId, msgId, requestType));
                    handler.Dequeue();
                    requestCount--;
                }
            }

            // getting requests that were requested but did not get any reply
            while (requestCount > 0)
            {
                Tuple<int, int, ulong, MessageStatus>? request = null;
                RequestType type = 0; // this is wrong, but for the sake of implementation
                foreach (var requestType in _orderedRequestTypes)
                {
                    var handler = GetMessageHandler(requestType);
                    var peekRequest = handler.Peek();
                    if (!(peekRequest is null))
                    {
                        // taking request that was not requested recently
                        if ((request is null) || request.Item3 > peekRequest.Item3)
                        {
                            request = peekRequest;
                            type = requestType;
                        }
                    }
                }
                if (!(request is null))
                {
                    var (validatorId, msgId, _, _) = request;
                    processedRequest.Add(Tuple.Create(validatorId, msgId, type));
                    var handler = GetMessageHandler(type);
                    handler.Dequeue();
                    requestCount--;
                }
                else break;
            }

            var currentTime = TimeUtils.CurrentTimeMillis();
            foreach (var (validatorId, msgId, type) in processedRequest)
            {
                var handler = GetMessageHandler(type);
                // enqueueing them to request again
                handler.Enqueue(validatorId, msgId, currentTime);
                handler.MessageRequested(validatorId, msgId);
                var msg = handler.CreateConsensusRequestMessage(_protocolId, msgId);
                var requestingTo = type == RequestType.Val ? msg.RequestConsensus.RequestVal.SenderId : validatorId;
                Logger.LogTrace($"Requesting consensus message {type} of id {msgId} for {_protocolId} to validator {requestingTo}");
                allRequests.Add((msg, requestingTo));
            }
            return allRequests;
        }

        private IMessageRequestHandler GetMessageHandler(RequestType type)
        {
            if (!_messageHandlers.TryGetValue((byte) type, out var handler))
                throw new Exception($"MessageRequestHandler {type} not registered");
            return handler;
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