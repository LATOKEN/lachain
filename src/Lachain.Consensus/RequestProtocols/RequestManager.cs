using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Lachain.Consensus.RequestProtocols.Messages;
using Lachain.Consensus.RequestProtocols.Messages.Requests;
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
        private readonly Thread _thread;
        private readonly object _queueLock = new object();
        private readonly Queue<(int, ConsensusMessage)> _queue;
        private bool _terminated = false;
        public RequestManager(IConsensusBroadcaster broadcaster, long era)
        {
            _broadcaster = broadcaster;
            _era = era;
            _protocolRequestHandler = new ConcurrentDictionary<IProtocolIdentifier, IProtocolRequestHandler>();
            _protocolResendHandler = new ConcurrentDictionary<IProtocolIdentifier, IProtocolResendHandler>();
            _queue = new Queue<(int, ConsensusMessage)>();
            _thread = new Thread(Start) {IsBackground = true};
            _thread.Start();
        }

        public void Terminate()
        {
            lock (_queueLock)
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
                _queue.Clear();
                Monitor.PulseAll(_queueLock);
                Logger.LogTrace($"Request manager for era {_era} terminated");
            }
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

        public void HandleRequest(int from, ConsensusMessage request)
        {
            lock (_queueLock)
            {
                if (_terminated)
                {
                    Monitor.PulseAll(_queueLock);
                    return;
                }
                _queue.Enqueue((from, request));
                Monitor.PulseAll(_queueLock);
            }
        }

        public void Start()
        {
            while (!_terminated)
            {
                ConsensusMessage msg;
                int from;
                lock (_queueLock)
                {
                    while (_queue.Count == 0 && !_terminated)
                    {
                        Monitor.Wait(_queueLock);
                    }

                    if (_terminated)
                        return;

                    (from, msg) = _queue.Dequeue();
                }

                try
                {
                    ProcessRequest(from, msg.RequestConsensus, msg.Validator.Era);
                }
                catch (Exception e)
                {
                    Logger.LogError($"RequestManager: exception occured while processing message: {e}");
                    Terminate();
                    break;
                }
            }
        }

        private void ProcessRequest(int from, RequestConsensusMessage request, long era)
        {
            IProtocolIdentifier protocolId;
            RequestType type;
            switch (request.PayloadCase)
            {
                case RequestConsensusMessage.PayloadOneofCase.RequestAux:
                    protocolId = AuxRequest.CreateProtocolId(request, era);
                    type = RequestType.Aux;
                    break;
                case RequestConsensusMessage.PayloadOneofCase.RequestBval:
                    protocolId = BValRequest.CreateProtocolId(request, era);
                    type = RequestType.Bval;
                    break;
                case RequestConsensusMessage.PayloadOneofCase.RequestCoin:
                    protocolId = CoinRequest.CreateProtocolId(request, era);
                    type = RequestType.Coin;
                    break;
                case RequestConsensusMessage.PayloadOneofCase.RequestConf:
                    protocolId = ConfRequest.CreateProtocolId(request, era);
                    type = RequestType.Conf;
                    break;
                case RequestConsensusMessage.PayloadOneofCase.RequestDecrypted:
                    protocolId = DecryptedRequest.CreateProtocolId(request, era);
                    type = RequestType.Decrypted;
                    break;
                case RequestConsensusMessage.PayloadOneofCase.RequestEcho:
                    protocolId = EchoRequest.CreateProtocolId(request, era);
                    type = RequestType.Echo;
                    break;
                case RequestConsensusMessage.PayloadOneofCase.RequestReady:
                    protocolId = ReadyRequest.CreateProtocolId(request, era);
                    type = RequestType.Ready;
                    break;
                case RequestConsensusMessage.PayloadOneofCase.RequestSignedHeader:
                    protocolId = SignedHeaderRequest.CreateProtocolId(request, era);
                    type = RequestType.SignedHeader;
                    break;
                case RequestConsensusMessage.PayloadOneofCase.RequestVal:
                    protocolId = ValRequest.CreateProtocolId(request, era);
                    type = RequestType.Val;
                    break;
                default:
                    var publicKey = _broadcaster.GetPublicKeyById(from);
                    Logger.LogError($"validator {publicKey} ({from}) sent request {request.PayloadCase} but we do not implement this request");
                    return;
            }

            if (_protocolResendHandler.TryGetValue(protocolId, out var handler))
            {
                try
                {
                    var response = handler.HandleRequest(from, request, type);
                    foreach (var msg in response)
                    {
                        _broadcaster.SendToValidator(msg, from);
                    }
                }
                catch (Exception exc)
                {
                    Logger.LogWarning($"Got exception trying to process request {type} for protocol {protocolId}: {exc}");
                }
            }
            else Logger.LogTrace($"{protocolId} not registered yet, discarding request {type}");
        }
    }
}