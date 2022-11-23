using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Lachain.Logger;
using Lachain.Consensus.Messages;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Consensus.BinaryAgreement
{
    public class BinaryBroadcast : AbstractProtocol
    {
        private static readonly ILogger<BinaryBroadcast> Logger = LoggerFactory.GetLoggerForClass<BinaryBroadcast>();

        private readonly BinaryBroadcastId _broadcastId;
        private BoolSet _binValues;
        private readonly BoolSet[] _receivedValues;
        private readonly int[] _receivedCount;
        private readonly bool[] _playerSentAux;
        private readonly bool[] _validatorSentConf;
        private readonly int[] _receivedAux;
        private readonly bool[] _wasBvalBroadcasted;
        private readonly List<BoolSet> _confReceived;
        private bool _confSent;
        private ResultStatus _requested;
        private BoolSet? _result;
        

        public BinaryBroadcast(
            BinaryBroadcastId broadcastId, IPublicConsensusKeySet wallet, IConsensusBroadcaster broadcaster)
            : base(wallet, broadcastId, broadcaster)
        {
            _broadcastId = broadcastId;
            _requested = ResultStatus.NotRequested;

            _binValues = new BoolSet();
            _receivedValues = new BoolSet[N];
            _playerSentAux = new bool[N];
            _validatorSentConf = new bool[N];
            for (var i = 0; i < N; ++i)
                _receivedValues[i] = new BoolSet();
            _receivedCount = new int[2];
            _receivedAux = new int[2];
            _wasBvalBroadcasted = new bool[2];
            _confReceived = new List<BoolSet>();
            _result = null;
            _confSent = false;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public override void ProcessMessage(MessageEnvelope envelope)
        {
            if (envelope.External)
            {
                var message = envelope.ExternalMessage;
                if (message is null)
                {
                    _lastMessage = "Failed to decode external message";
                    throw new ArgumentNullException();
                }

                if (message.Validator.Era != Id.Era)
                {
                    _lastMessage = $"Era mismatch: our era is {Id.Era}, message era is {message.Validator.Era}";
                    throw new ArgumentException("era mismatched");
                }
                switch (message.PayloadCase)
                {
                    case ConsensusMessage.PayloadOneofCase.Bval:
                        _lastMessage = "BValMessage";
                        HandleBValMessage(envelope.ValidatorIndex, message.Bval);
                        return;
                    case ConsensusMessage.PayloadOneofCase.Aux:
                        _lastMessage = "AuxMessage";
                        HandleAuxMessage(envelope.ValidatorIndex, message.Aux);
                        return;
                    case ConsensusMessage.PayloadOneofCase.Conf:
                        _lastMessage = "ConfMessage";
                        HandleConfMessage(envelope.ValidatorIndex, message.Conf);
                        return;
                    default:
                        _lastMessage =
                            $"consensus message of type {message.PayloadCase} routed to BinaryBroadcast protocol";
                        throw new ArgumentException(
                            $"consensus message of type {message.PayloadCase} routed to BinaryBroadcast protocol"
                        );
                }
            }
            else
            {
                var message = envelope.InternalMessage;
                switch (message)
                {
                    case ProtocolRequest<BinaryBroadcastId, bool> broadcastRequested:
                        _lastMessage = "broadcastRequested";
                        HandleRequest(broadcastRequested);
                        break;
                    case ProtocolResult<BinaryBroadcastId, BoolSet> _:
                        _lastMessage = "ProtocolResult";
                        Terminate();
                        break;
                    default:
                        _lastMessage = "Binary broadcast protocol handles not any internal messages";
                        throw new InvalidOperationException(
                            "Binary broadcast protocol handles not any internal messages");
                }
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void HandleRequest(ProtocolRequest<BinaryBroadcastId, bool> broadcastRequested)
        {
            _requested = ResultStatus.Requested;
            CheckResult();
            BroadcastBVal(broadcastRequested.Input);
        }

        private void BroadcastBVal(bool value)
        {
            var b = value ? 1 : 0;
            _wasBvalBroadcasted[b] = true;
            var msg = CreateBValMessage(b);
            Broadcaster.Broadcast(msg);
            InvokeMessageBroadcasted(msg);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void HandleBValMessage(int sender, BValMessage bval)
        {
            if (bval.Epoch != _broadcastId.Epoch || bval.Agreement != _broadcastId.Agreement)
                throw new ArgumentException("era, agreement or epoch of message mismatched");

            var b = bval.Value ? 1 : 0;

            if (_receivedValues[sender].Contains(b == 1))
            {
                Logger.LogTrace($"{_broadcastId}: double receive message {bval} from {sender}");
                return;
            }

            _receivedValues[sender].Add(b == 1);
            ++_receivedCount[b];
            InvokeReceivedExternalMessage(sender, new ConsensusMessage { Bval = bval });

            if (!_wasBvalBroadcasted[b] && _receivedCount[b] >= F + 1)
            {
                BroadcastBVal(bval.Value);
            }

            if (_receivedCount[b] < 2 * F + 1) return;
            if (_binValues.Contains(b == 1)) return;

            _binValues = _binValues.Add(b == 1);
            if (_binValues.Count() == 1)
            {
                var msg = CreateAuxMessage(b);
                Broadcaster.Broadcast(msg);
                InvokeMessageBroadcasted(msg);
            }

            RevisitAuxMessages();
            RevisitConfMessages();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void HandleAuxMessage(int sender, AuxMessage aux)
        {
            if (aux.Epoch != _broadcastId.Epoch || aux.Agreement != _broadcastId.Agreement)
                throw new ArgumentException("era, agreement or epoch of message mismatched");

            var b = aux.Value ? 1 : 0;
            if (_playerSentAux[sender])
            {
                Logger.LogTrace($"{_broadcastId}: double receive message {aux} from {sender}");
                return;
            }

            _playerSentAux[sender] = true;
            _receivedAux[b]++;
            InvokeReceivedExternalMessage(sender, new ConsensusMessage { Aux = aux });
            RevisitAuxMessages();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void HandleConfMessage(int sender, ConfMessage conf)
        {
            if (conf.Epoch != _broadcastId.Epoch || conf.Agreement != _broadcastId.Agreement)
                throw new ArgumentException("era, agreement or epoch of message mismatched");

            if (_validatorSentConf[sender])
            {
                Logger.LogTrace($"{_broadcastId}: double receive message {conf} from {sender}");
                return;
            }

            _validatorSentConf[sender] = true;

            _confReceived.Add(new BoolSet(conf.Values));
            InvokeReceivedExternalMessage(sender, new ConsensusMessage { Conf = conf });
            RevisitConfMessages();
        }

        private BoolSet ChoseMinimalSet()
        {
            return _binValues;
            // TODO: investigate if choosing minimal set speed up execution. This should not break protocol integrity
            // if (_binValues.Values().Sum(b => _receivedAux[b ? 1 : 0]) < N - F)
            //     throw new Exception($"Player {GetMyId()} at {_broadcastId}: can't choose minimal set: unsufficient auxs!");
            // if (_confReceived.Count(set => _binValues.Contains(set)) < N - F)
            //     throw new Exception($"Player {GetMyId()} at {_broadcastId}: can't choose minimal set: unsufficient confs!");
            //
            // foreach (var b in _binValues.Values())
            // {
            //     if (_receivedAux[b ? 1 : 0] >= N - F) // && _confReceived.Where(set => _binValues.Contains(set)).Count(set => set.Contains(b)) >= N - F)
            //         return new BoolSet(b);
            // }
            //
            // return _binValues;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void RevisitConfMessages()
        {
            // TODO: investigate relation between _confReceived, _binValues and _result
            var goodConfs = _confReceived.Count(set => _binValues.Contains(set));
            if (goodConfs < N - F) return;
            if (_result != null) return;
            if (_binValues.Values().Sum(b => _receivedAux[b ? 1 : 0]) < N - F) return;
            _result = ChoseMinimalSet();
        //    Logger.LogTrace($"{_broadcastId}: aux cnt = 0 -> {_receivedAux[0]}, 1 -> {_receivedAux[1]}");
        //    Logger.LogTrace($"{_broadcastId}: current bin_values = {_binValues}");
        //    Logger.LogTrace($"{_broadcastId}: and sum of aux on bin_values is {_binValues.Values().Sum(b => _receivedAux[b ? 1 : 0])}");
            CheckResult();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void RevisitAuxMessages()
        {
            if (_confSent) return;
            if (_binValues.Values().Sum(b => _receivedAux[b ? 1 : 0]) < N - F) return;
            Logger.LogTrace($"{_broadcastId}: conf message sent with set {_binValues}");
            var msg = CreateConfMessage(_binValues);
            Broadcaster.Broadcast(msg);
            InvokeMessageBroadcasted(msg);
            _confSent = true;
            RevisitConfMessages();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private ConsensusMessage CreateBValMessage(int value)
        {
            var message = new ConsensusMessage
            {
                Bval = new BValMessage
                {
                    Agreement = _broadcastId.Agreement,
                    Epoch = _broadcastId.Epoch,
                    Value = value == 1
                }
            };
            return message;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private ConsensusMessage CreateAuxMessage(int value)
        {
            var message = new ConsensusMessage
            {
                Aux = new AuxMessage
                {
                    Agreement = _broadcastId.Agreement,
                    Epoch = _broadcastId.Epoch,
                    Value = value == 1
                }
            };
            return message;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private ConsensusMessage CreateConfMessage(BoolSet values)
        {
            var message = new ConsensusMessage
            {
                Conf = new ConfMessage
                {
                    Agreement = _broadcastId.Agreement,
                    Epoch = _broadcastId.Epoch,
                    Values = {values.Values()}
                }
            };
            return message;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void CheckResult()
        {
            if (!_result.HasValue) return;
            if (_requested != ResultStatus.Requested) return;
            Logger.LogTrace($"{_broadcastId}: made result {_result.Value.ToString()}");
            Broadcaster.InternalResponse(
                new ProtocolResult<BinaryBroadcastId, BoolSet>(_broadcastId, _result.Value));
            _requested = ResultStatus.Sent;
        }
    }
}