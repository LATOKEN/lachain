using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Phorkus.Consensus.Messages;
using Phorkus.Proto;
using Phorkus.Utility.Utils;

namespace Phorkus.Consensus.BinaryAgreement
{
    public class BinaryBroadcast : AbstractProtocol
    {
        private readonly BinaryBroadcastId _broadcastId;
        private BoolSet _binValues;
        private readonly ISet<int>[] _receivedValues;
        private readonly int[] _receivedCount;
        private readonly bool[] _playerSentAux;
        private readonly bool[] _validatorSentConf;
        private readonly int[] _receivedAux;
        private readonly bool[] _wasBvalBroadcasted;
        private readonly List<BoolSet> _confReceived;
        private bool _auxSent;
        private bool _confSent;
        private ResultStatus _requested;
        private BoolSet? _result;

        public override IProtocolIdentifier Id => _broadcastId;

        public BinaryBroadcast(BinaryBroadcastId broadcastId, IWallet wallet, IConsensusBroadcaster broadcaster)
        : base(wallet, broadcaster)
        {
            _broadcastId = broadcastId;
            _requested = ResultStatus.NotRequested;

            _binValues = new BoolSet();
            _receivedValues = new ISet<int>[N];
            _playerSentAux = new bool[N];
            _validatorSentConf = new bool[N];
            for (var i = 0; i < N; ++i)
                _receivedValues[i] = new HashSet<int>();
            _receivedCount = new int[2];
            _receivedAux = new int[2];
            _wasBvalBroadcasted = new bool[2];
            _auxSent = false;
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
                switch (message.PayloadCase)
                {
                    case ConsensusMessage.PayloadOneofCase.Bval:
                        HandleBValMessage(message.Validator, message.Bval);
                        return;
                    case ConsensusMessage.PayloadOneofCase.Aux:
                        HandleAuxMessage(message.Validator, message.Aux);
                        return;
                    case ConsensusMessage.PayloadOneofCase.Conf:
                        HandleConfMessage(message.Validator, message.Conf);
                        return;
                    default:
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
                        HandleRequest(broadcastRequested);
                        break;
                    case ProtocolResult<BinaryBroadcastId, BoolSet> _:
                        Terminated = true;
                        break;
                    default:
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
            _broadcaster.Broadcast(CreateBValMessage(b));
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void HandleBValMessage(Validator validator, BValMessage bval)
        {
            // todo investigate reason for this
            //  if (_auxSent) return;
            if (
                validator.Era != _broadcastId.Era || bval.Epoch != _broadcastId.Epoch ||
                bval.Agreement != _broadcastId.Agreement
            )
                throw new ArgumentException("era, agreement or epoch of message mismatched");

            var sender = validator.ValidatorIndex;
            var b = bval.Value ? 1 : 0;

            if (_receivedValues[sender].Contains(b))
            {
                // todo write fault evidence management = logging
                Console.Error.WriteLine($"Player {GetMyId()} at {_broadcastId}: double receive message {bval} from {sender}");
                return; // potential fault evidence
            }
            _receivedValues[sender].Add(b);
            ++_receivedCount[b];

            if (!_wasBvalBroadcasted[b] && _receivedCount[b] >= F + 1)
            {
                BroadcastBVal(bval.Value);
            }

            if (_receivedCount[b] < 2 * F + 1) return;
            if (_binValues.Contains(b == 1)) return;
            
            _binValues = _binValues.Add(b == 1);
            // todo investigate
            if (_binValues.Count() == 1)
            {
                _broadcaster.Broadcast(CreateAuxMessage(b));
                _auxSent = true;
            }

            RevisitAuxMessages();
            RevisitConfMessages();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void HandleAuxMessage(Validator validator, AuxMessage aux)
        {
            if (
                validator.Era != _broadcastId.Era || aux.Epoch != _broadcastId.Epoch ||
                aux.Agreement != _broadcastId.Agreement
            )
                throw new ArgumentException("era, agreement or epoch of message mismatched");

            var sender = validator.ValidatorIndex;
            var b = aux.Value ? 1 : 0;
            if (_playerSentAux[sender])
            {
                return; // potential fault evidence
            }

            _playerSentAux[sender] = true;
            _receivedAux[b]++;
            RevisitAuxMessages();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void HandleConfMessage(Validator validator, ConfMessage conf)
        {
            if (
                validator.Era != _broadcastId.Era || conf.Epoch != _broadcastId.Epoch ||
                conf.Agreement != _broadcastId.Agreement
            )
                throw new ArgumentException("era, agreement or epoch of message mismatched");

            var sender = validator.ValidatorIndex;

            if (_validatorSentConf[sender]) return; // potential fault evidence
            _validatorSentConf[sender] = true;

            _confReceived.Add(new BoolSet(conf.Values));
            RevisitConfMessages();
        }

        private BoolSet ChoseMinimalSet()
        {
            if (_binValues.Values().Sum(b => _receivedAux[b ? 1 : 0]) < N - F)
                throw new Exception($"Player {GetMyId()} at {_broadcastId}: cant chose minimal set!");
            
            foreach (var b in _binValues.Values())
            {
                if (_receivedAux[b ? 1 : 0] >= N - F)
                    return new BoolSet(b);
            }

            return _binValues;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void RevisitConfMessages()
        {
            var goodConfs = _confReceived.Count(set => _binValues.Contains(set));
            if (goodConfs < N - F) return;
            if (_result != null) return;
            if (_binValues.Values().Sum(b => _receivedAux[b ? 1 : 0]) < N - F) return;
            _result = ChoseMinimalSet();
            Console.Error.WriteLine($"Player {GetMyId()} at {_broadcastId}: aux cnt = 0 -> {_receivedAux[0]}, 1 -> {_receivedAux[1]}");
            Console.Error.WriteLine($"Player {GetMyId()} at {_broadcastId}: my current bin_values = {_binValues}");
            Console.Error.WriteLine($"Player {GetMyId()} at {_broadcastId}: and sum of aux on bin_values is {_binValues.Values().Sum(b => _receivedAux[b ? 1 : 0])}");
            CheckResult();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void RevisitAuxMessages()
        {
            if (_confSent) return;
            if (_binValues.Values().Sum(b => _receivedAux[b ? 1 : 0]) < N - F) return;
            Console.Error.WriteLine($"Player {GetMyId()} at {_broadcastId}: conf message sent with set {_binValues}");
            _broadcaster.Broadcast(CreateConfMessage(_binValues));
            _confSent = true;
            RevisitConfMessages();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private ConsensusMessage CreateBValMessage(int value)
        {
            var message = new ConsensusMessage
            {
                Validator = new Validator
                {
                    // TODO: somehow fill validator field
                    ValidatorIndex = GetMyId(),
                    Era = _broadcastId.Era
                },
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
                Validator = new Validator
                {
                    // TODO: somehow fill validator field
                    ValidatorIndex = GetMyId(),
                    Era = _broadcastId.Era
                },
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
                Validator = new Validator
                {
                    // TODO: somehow fill validator field
                    ValidatorIndex = GetMyId(),
                    Era = _broadcastId.Era
                },
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
            if (_result == null) return;
            if (_requested != ResultStatus.Requested) return;
            _broadcaster.InternalResponse(
                new ProtocolResult<BinaryBroadcastId, BoolSet>(_broadcastId, _result.Value));
            Console.Error.WriteLine($"Player {GetMyId()} at {_broadcastId}: made result {_result.Value.ToString()}");
            _requested = ResultStatus.Sent;
        }
    }
}