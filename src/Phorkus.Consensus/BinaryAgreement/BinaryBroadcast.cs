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
        private readonly IConsensusBroadcaster _broadcaster;
        private BoolSet _binValues;
        private readonly int _faulty, _players;
        private readonly ISet<int>[] _receivedValues;
        private readonly int[] _receivedCount;
        private readonly bool[] _validatorSentAux;
        private readonly bool[] _validatorSentConf;
        private readonly int[] _receivedAux;
        private readonly bool[] _isBroadcast;
        private readonly List<BoolSet> _confReceived;
        private bool _auxSent;
        private ResultStatus _requested;
        private BoolSet? _result;

        public override IProtocolIdentifier Id => _broadcastId;

        public BinaryBroadcast(int n, int f, BinaryBroadcastId broadcastId, IConsensusBroadcaster broadcaster)
        {
            _broadcastId = broadcastId;
            _broadcaster = broadcaster;
            _players = n;
            _faulty = f;
            _requested = ResultStatus.NotRequested;

            _binValues = new BoolSet();
            _receivedValues = new ISet<int>[n];
            _validatorSentAux = new bool[n];
            _validatorSentConf = new bool[n];
            for (var i = 0; i < n; ++i)
                _receivedValues[i] = new HashSet<int>();
            _receivedCount = new int[2];
            _receivedAux = new int[2];
            _isBroadcast = new bool[2];
            _auxSent = false;
            _confReceived = new List<BoolSet>();
            _result = null;
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
//                        Console.Error.WriteLine(
//                            $"{_consensusBroadcaster.GetMyId()}: BVal from {message.Validator.ValidatorIndex}"
//                        );
                        HandleBValMessage(message.Validator, message.Bval);
                        return;
                    case ConsensusMessage.PayloadOneofCase.Aux:
//                        Console.Error.WriteLine(
//                            $"{_consensusBroadcaster.GetMyId()}: Aux from {message.Validator.ValidatorIndex}"
//                        );
                        HandleAuxMessage(message.Validator, message.Aux);
                        return;
                    case ConsensusMessage.PayloadOneofCase.Conf:
//                        Console.Error.WriteLine(
//                            $"{_consensusBroadcaster.GetMyId()}: Conf from {message.Validator.ValidatorIndex}"
//                        );
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
//                        Console.Error.WriteLine($"{_consensusBroadcaster.GetMyId()}: broadcast requested");
                        _requested = ResultStatus.Requested;
                        CheckResult();
                        var b = broadcastRequested.Input ? 1 : 0;
                        _isBroadcast[b] = true;
                        _broadcaster.Broadcast(CreateBValMessage(b));
                        break;
                    case ProtocolResult<BinaryBroadcastId, BoolSet> _:
//                        Console.Error.WriteLine($"{_consensusBroadcaster.GetMyId()}: broadcast completed");
                        Terminated = true;
                        break;
                    default:
                        throw new InvalidOperationException(
                            "Binary broadcast protocol handles not any internal messages");
                }
            }
        }

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

            // if (_receivedValues[sender].Contains(b)) return; // potential fault evidence
            _receivedValues[sender].Add(b);
            ++_receivedCount[b];

            if (!_isBroadcast[b] && _receivedCount[b] >= _faulty + 1)
            {
                _isBroadcast[b] = true;
                _broadcaster.Broadcast(CreateBValMessage(b));
            }

            if (_receivedCount[b] < 2 * _faulty + 1) return;

            // todo wtf
            if (_binValues.Contains(b == 1)) return;
            _binValues = _binValues.Add(b == 1);
            // investigate
            if (_binValues.Count() == 1)
            {
                _auxSent = true;
                _broadcaster.Broadcast(CreateAuxMessage(b));
            }

            RevisitAuxMessages();
            RevisitConfMessages();
        }

        private void HandleAuxMessage(Validator validator, AuxMessage aux)
        {
            if (
                validator.Era != _broadcastId.Era || aux.Epoch != _broadcastId.Epoch ||
                aux.Agreement != _broadcastId.Agreement
            )
                throw new ArgumentException("era, agreement or epoch of message mismatched");

            var sender = validator.ValidatorIndex;
            var b = aux.Value ? 1 : 0;
            if (_validatorSentAux[sender])
            {
                return; // potential fault evidence
            }

            _validatorSentAux[sender] = true;
            _receivedAux[b]++;
            RevisitAuxMessages();
        }

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

        private void RevisitConfMessages()
        {
            if (!_auxSent) return;
            var goodConfs = _confReceived.Count(set => _binValues.Contains(set));
            if (goodConfs < _players - _faulty) return;
            _result = _binValues;
            CheckResult();
        }

        private void RevisitAuxMessages()
        {
            if (_binValues.Values().Sum(b => _receivedAux[b ? 1 : 0]) < _players - _faulty) return;
            _broadcaster.Broadcast(CreateConfMessage(_binValues));
        }

        private ConsensusMessage CreateBValMessage(int value)
        {
            var message = new ConsensusMessage
            {
                Validator = new Validator
                {
                    // TODO: somehow fill validator field
                    ValidatorIndex = _broadcaster.GetMyId(),
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

        private ConsensusMessage CreateAuxMessage(int value)
        {
            var message = new ConsensusMessage
            {
                Validator = new Validator
                {
                    // TODO: somehow fill validator field
                    ValidatorIndex = _broadcaster.GetMyId(),
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

        private ConsensusMessage CreateConfMessage(BoolSet values)
        {
            var message = new ConsensusMessage
            {
                Validator = new Validator
                {
                    // TODO: somehow fill validator field
                    ValidatorIndex = _broadcaster.GetMyId(),
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

        private void CheckResult()
        {
            if (_result == null) return;
            if (_requested == ResultStatus.Requested)
            {
                _requested = ResultStatus.Sent;
                _broadcaster.InternalResponse(
                    new ProtocolResult<BinaryBroadcastId, BoolSet>(_broadcastId, _binValues));
            }
        }
    }
}