using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Lachain.Consensus.RequestProtocols.Protocols;
using Lachain.Logger;
using Lachain.Proto;

namespace Lachain.Consensus.RequestProtocols
{
    public class RequestManager : IRequestManager
    {
        private static readonly ILogger<RequestManager> Logger = LoggerFactory.GetLoggerForClass<RequestManager>();
        private int _validators = -1;
        private readonly long _era;
        private readonly IConsensusBroadcaster _broadcaster;
        private readonly IDictionary<IProtocolIdentifier, IProtocolRequestHandler> _protocolRequestHandler;
        private readonly IDictionary<IProtocolIdentifier, IProtocolResendHandler> _protocolResendHandler;
        private bool _terminated = false;
        public RequestManager(IConsensusBroadcaster broadcaster, long era)
        {
            _broadcaster = broadcaster;
            _era = era;
            _protocolRequestHandler = new ConcurrentDictionary<IProtocolIdentifier, IProtocolRequestHandler>();
            _protocolResendHandler = new ConcurrentDictionary<IProtocolIdentifier, IProtocolResendHandler>();
        }

        public void Terminate()
        {
            if (_terminated)
                return;
            _terminated = true;
            foreach (var (_, handler) in _protocolRequestHandler)
            {
                handler.Terminate();
            }
            _protocolRequestHandler.Clear();
            foreach (var (_, handler) in _protocolResendHandler)
            {
                handler.Terminate();
            }
            _protocolResendHandler.Clear();
            Logger.LogTrace($"Request manager for era {_era} terminated");
        }

        public void SetValidators(int validatorsCount)
        {
            _validators = validatorsCount;
        }

        public void RegisterProtocol(IProtocolIdentifier protocolId)
        {
            if (_terminated)
                return;
            if (_validators == -1)
                throw new Exception($"RequestManager not ready yet, validators count {_validators}");
            if (_protocolRequestHandler.TryGetValue(protocolId, out var _))
            {
                throw new Exception($"Protocol handler for protocolId {protocolId} already registered");
            }

            _protocolRequestHandler[protocolId] = new ProtocolRequestHandler(protocolId, _validators);

            if (_protocolResendHandler.TryGetValue(protocolId, out var _))
            {
                throw new Exception($"Protocol handler for protocolId {protocolId} already registered");
            }

            _protocolResendHandler[protocolId] = new ProtocolResendHandler(protocolId, _validators);
        }
    }
}