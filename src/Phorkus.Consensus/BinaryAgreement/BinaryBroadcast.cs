using System;
using System.Collections.Generic;
using Phorkus.Proto;

namespace Phorkus.Consensus.BinaryAgreement
{
    public class BinaryBroadcast : IBinaryBroadcast
    {
        private readonly BinaryBroadcastId _broadcastId;
        private readonly IConsensusBroadcaster _consensusBroadcaster;
        
        private int _binValuesMask;
        private readonly ISet<int>[] _recievedValues;
        private readonly int[] _recievedCount;
        private readonly int _faulty;
        private readonly bool[] _broadcasted;
        private bool _terminated;

        public BinaryBroadcast(int n, int f, BinaryBroadcastId broadcastId, IConsensusBroadcaster consensusBroadcaster)
        {
            _broadcastId = broadcastId;
            _consensusBroadcaster = consensusBroadcaster;
            _binValuesMask = 0;
            _faulty = f;
            _recievedValues = new ISet<int>[n];
            for (var i = 0; i < n; ++i)
                _recievedValues[i] = new HashSet<int>();
            _recievedCount = new int[2];
            _broadcasted = new bool[2];
            _terminated = false;
        }
        
        public void Input(int value)
        {
            if (_terminated) return;
            if (value < 0 || value > 1) throw new ArgumentOutOfRangeException(nameof(value));
            _broadcasted[value] = true;
            _consensusBroadcaster.Broadcast(CreateBValMessage(value));
        }

        public event EventHandler<int> BinValueAdded;
        public event EventHandler Terminated;
        
        public void HandleMessage(ConsensusMessage message)
        {
            if (_terminated) return;
            if (message.PayloadCase != ConsensusMessage.PayloadOneofCase.BinaryBroadcast)
                throw new ArgumentException($"consensus message of type {message.PayloadCase} misrouted to BinaryBroadcast protcol");
            var sender = message.Validator.ValidatorIndex;
            var b = message.BinaryBroadcast.Value ? 1 : 0;
            if (_recievedValues[sender].Contains(b)) return;
            _recievedValues[sender].Add(b);
            ++_recievedCount[b];

            if (!_broadcasted[b] && _recievedCount[b] >= _faulty + 1)
            {
                _broadcasted[b] = true;
                _consensusBroadcaster.Broadcast(CreateBValMessage(b));
            }

            if (_recievedCount[b] < 2 * _faulty + 1) return;
            
            var newMask = _binValuesMask | (1 << b);
            if (newMask == _binValuesMask) return;
            _binValuesMask = newMask;
            BinValueAdded?.Invoke(this, b);
            if (newMask == 3)
            {
                _terminated = true;
                Terminated?.Invoke(this, null);
            }
        }

        public void Terminate()
        {
            if (_terminated) return;
            _terminated = true;
            Terminated?.Invoke(this, null);
        }

        private ConsensusMessage CreateBValMessage(int value)
        {
            var message = new ConsensusMessage
            {
                Validator = new Validator
                {
                    // TODO: somehow fill validator field
                    Epoch = _broadcastId.Epoch
                },
                BinaryBroadcast = new Proto.BinaryBroadcast
                {
                    Agreement = _broadcastId.Agreement,
                    Round = _broadcastId.Round,
                    Value = value == 1
                }
            };
            return message;
        }
    }
}