using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Phorkus.Consensus.Messages;
using Phorkus.Proto;
using Phorkus.Utility.Utils;

namespace Phorkus.Consensus.BinaryAgreement
{
    public class BinaryBroadcast : IBinaryBroadcast
    {
        private readonly BinaryBroadcastId _broadcastId;
        private readonly IConsensusBroadcaster _consensusBroadcaster;
        private BoolSet _binValues;
        private readonly int _faulty, _players;
        private readonly ISet<int>[] _receivedValues;
        private readonly int[] _receivedCount;
        private readonly bool[] _validatorSentAux;
        private readonly bool[] _validatorSentConf;
        private readonly int[] _receivedAux;
        private readonly bool[] _isBroadcast;
        private readonly List<BoolSet> _confReceived;
        private bool _terminated;
        private bool _auxSent;
        private BoolSet _result;

        public IProtocolIdentifier Id => _broadcastId;

        public BinaryBroadcast(int n, int f, BinaryBroadcastId broadcastId, IConsensusBroadcaster consensusBroadcaster)
        {
            _broadcastId = broadcastId;
            _consensusBroadcaster = consensusBroadcaster;
            _players = n;
            _faulty = f;

            _binValues = new BoolSet();
            _receivedValues = new ISet<int>[n];
            _validatorSentAux = new bool[n];
            _validatorSentConf = new bool[n];
            for (var i = 0; i < n; ++i)
                _receivedValues[i] = new HashSet<int>();
            _receivedCount = new int[2];
            _receivedAux = new int[2];
            _isBroadcast = new bool[2];
            _terminated = false;
            _auxSent = false;
            _confReceived = new List<BoolSet>();

        }

        public uint GetMyId()
        {
            return _consensusBroadcaster.GetMyId();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Input(bool value)
        {
            if (_terminated) return;
            var b = value ? 1 : 0;
            _isBroadcast[b] = true;
            _consensusBroadcaster.Broadcast(CreateBValMessage(b));
        }

        public bool Terminated(out BoolSet values)
        {
            values = _result;
            return _terminated;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void HandleMessage(ConsensusMessage message)
        {
            if (_terminated) return;
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

        public void HandleInternalMessage(InternalMessage message)
        {
            switch (message)
            {
                 case BroadcastCompleted completed:
                     _result = completed.Values;
                     _terminated = true;
                     break;
                 default:
                    throw new InvalidOperationException("Binary broadcast protocol handles not any internal messages");
            }
        }

        private void HandleBValMessage(Validator validator, BValMessage bval)
        {
            // todo investigate reason for this
//            if (_auxSent) return;
            if (
                validator.Era != _broadcastId.Era || bval.Epoch != _broadcastId.Epoch ||
                bval.Agreement != _broadcastId.Agreement
            )
                throw new ArgumentException("era, agreement or epoch of message mismatched");

            var sender = validator.ValidatorIndex;
            var b = bval.Value ? 1 : 0;

//            if (_receivedValues[sender].Contains(b)) return; // potential fault evidence
            _receivedValues[sender].Add(b);
            ++_receivedCount[b];

            if (!_isBroadcast[b] && _receivedCount[b] >= _faulty + 1)
            {
                _isBroadcast[b] = true;
                _consensusBroadcaster.Broadcast(CreateBValMessage(b));
            }

            if (_receivedCount[b] < 2 * _faulty + 1) return;

//            todo wtf
            if (_binValues.Contains(b == 1)) return;
            _binValues = _binValues.Add(b == 1);
//            Console.Error.WriteLine($"Player {GetMyId()} added {b}");
            // investigate
//            if (true)
            if (_binValues.Count() == 1)
            {
                _auxSent = true;
                _consensusBroadcaster.Broadcast(CreateAuxMessage(b));
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
            
//            Console.Error.WriteLine($"Player {GetMyId()} received {b} from {sender}");

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
            if (_terminated || !_auxSent) return;
            var goodConfs = _confReceived.Count(set => _binValues.Contains(set));
            if (goodConfs < _players - _faulty) return;
            _consensusBroadcaster.MessageSelf(new BroadcastCompleted(_broadcastId, _binValues));
        }

        private void RevisitAuxMessages()
        {
            if (_terminated) return;
            if (_binValues.Values().Sum(b => _receivedAux[b ? 1 : 0]) < _players - _faulty) return;
            BroadcastConf(_binValues);
        }

        private void BroadcastConf(BoolSet values)
        {
            _consensusBroadcaster.Broadcast(CreateConfMessage(values));
        }

        private ConsensusMessage CreateBValMessage(int value)
        {
            var message = new ConsensusMessage
            {
                Validator = new Validator
                {
                    // TODO: somehow fill validator field
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
    }
}