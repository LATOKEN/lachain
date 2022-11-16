using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Lachain.Consensus.RequestProtocols.Messages;
using Lachain.Consensus.RequestProtocols.Messages.Resends;
using Lachain.Logger;
using Lachain.Proto;

namespace Lachain.Consensus.RequestProtocols.Protocols
{
    public class ProtocolResendHandler : IProtocolResendHandler
    {
        private static readonly ILogger<ProtocolResendHandler> Logger = LoggerFactory.GetLoggerForClass<ProtocolResendHandler>();
        private readonly IProtocolIdentifier _protocolId;
        private readonly int _validatorsCount;
        private readonly ProtocolType _type;
        private readonly IDictionary<byte, IMessageResendHandler> _messageHandlers;
        private bool _terminated = false;
        public ProtocolResendHandler(IProtocolIdentifier id, int validators)
        {
            _protocolId = id;
            _validatorsCount = validators;
            _type = ProtocolUtils.GetProtocolType(id);
            _messageHandlers = new ConcurrentDictionary<byte, IMessageResendHandler>();
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
        }

        public void Terminate()
        {
            if (_terminated)
                return;
            _terminated = true;
            foreach (var (_, handler) in _messageHandlers)
            {
                handler.Terminate();
            }
            _messageHandlers.Clear();
            Logger.LogTrace($"ProtocolResendHandler for protocol {_protocolId} terminated");
        }

        public void MessageSent(int validator, ConsensusMessage msg)
        {
            if (_terminated)
                return;
            var type = MessageUtils.GetRequestTypeForMessageType(msg);
            if (_messageHandlers.TryGetValue((byte) type, out var handler))
            {
                handler.MessageSent(validator, msg, type);
            }
            else throw new Exception($"MessageResendHandler {type} not registered");
        }

        public List<ConsensusMessage> HandleRequest(int from, RequestConsensusMessage request, RequestType requestType)
        {
            if (_messageHandlers.TryGetValue((byte) requestType, out var handler))
            {
                var response = handler.HandleRequest(from, request, requestType);
                var validResponse = new List<ConsensusMessage>();
                foreach (var msg in response)
                {
                    if (!(msg is null))
                        validResponse.Add(msg);
                }
                return validResponse;
            }
            throw new Exception($"MessageResendHandler {requestType} not registered");
        }

        private IMessageResendHandler RegisterMessageHandler(RequestType type)
        {
            switch (type)
            {
                case RequestType.Aux:
                    return new AuxResend(type, _validatorsCount, 1);
                case RequestType.Bval:
                    return new BValResend(type, _validatorsCount, 2);
                case RequestType.Coin:
                    return new CoinResend(type, _validatorsCount, 1);
                case RequestType.Conf:
                    return new ConfResend(type, _validatorsCount, 1);
                case RequestType.Decrypted:
                    return new DecryptedResend(type, _validatorsCount, _validatorsCount);
                case RequestType.Echo:
                    return new EchoResend(type, _validatorsCount, 1);
                case RequestType.Val:
                    return new ValResend(type, _validatorsCount, 1);
                case RequestType.Ready:
                    return new ReadyResend(type, _validatorsCount, 1);
                case RequestType.SignedHeader:
                    return new SignedHeaderResend(type, _validatorsCount, 1);
                default:
                    throw new Exception($"RegisterMessageHandler Not implemented for request type {type}");
            }
        }
    }
}