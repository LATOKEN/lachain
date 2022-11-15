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
        private readonly IDictionary<IProtocolIdentifier, IProtocolRequestHandler> _protocolHandler;
        private bool _terminated = false;
        public RequestManager(IConsensusBroadcaster broadcaster, long era)
        {
            _broadcaster = broadcaster;
            _era = era;
            _protocolHandler = new ConcurrentDictionary<IProtocolIdentifier, IProtocolRequestHandler>();
        }

        public void Terminate()
        {
            if (_terminated)
                return;
            _terminated = true;
            foreach (var (_, handler) in _protocolHandler)
            {
                handler.Terminate();
            }
            _protocolHandler.Clear();
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
            if (_protocolHandler.TryGetValue(protocolId, out var _))
            {
                throw new Exception($"Protocol handler for protocolId {protocolId} already registered");
            }

            _protocolHandler[protocolId] = new ProtocolRequestHandler(protocolId, _validators);
        }

        public void MessageReceived(IProtocolIdentifier protocolId, int from, ConsensusMessage msg)
        {
            if (_terminated)
                return;
            if (_protocolHandler.TryGetValue(protocolId, out var handler))
            {
                handler.MessageReceived(from, msg);
            }
            else
                throw new Exception($"Protocol handler for protocolId {protocolId} not registered but External message is received");
        }
    }
}