﻿using System;
using System.Collections.Generic;
using Phorkus.Proto;

namespace Phorkus.Consensus.BinaryAgreement
{
    public class BinaryBroadcast : IBinaryBroadcast
    {
        private readonly BinaryBroadcastId _broadcastId;
        private readonly IConsensusBroadcaster _consensusBroadcaster;

        private int _binValuesMask;
        private readonly ISet<int>[] _receivedValues;
        private readonly int[] _receivedCount;
        private readonly int _faulty;
        private readonly bool[] _broadcasted;
        private bool _terminated;

        public BinaryBroadcast(int n, int f, BinaryBroadcastId broadcastId, IConsensusBroadcaster consensusBroadcaster)
        {
            _broadcastId = broadcastId;
            _consensusBroadcaster = consensusBroadcaster;
            _binValuesMask = 0;
            _faulty = f;
            _receivedValues = new ISet<int>[n];
            for (var i = 0; i < n; ++i)
                _receivedValues[i] = new HashSet<int>();
            _receivedCount = new int[2];
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

        public IProtocolIdentifier Id => _broadcastId;

        public void HandleMessage(ConsensusMessage message)
        {
            if (_terminated) return;
            // These checks are somewhat redundant, but whatever
            if (message.PayloadCase != ConsensusMessage.PayloadOneofCase.BinaryBroadcast)
                throw new ArgumentException(
                    $"consensus message of type {message.PayloadCase} routed to BinaryBroadcast protocol");
            if (
                message.Validator.Era != _broadcastId.Era ||
                message.BinaryBroadcast.Epoch != _broadcastId.Epoch ||
                message.BinaryBroadcast.Agreement != _broadcastId.Agreement
            )
                throw new ArgumentException("era, agreement or epoch of message mismatched");

            var sender = message.Validator.ValidatorIndex;
            var b = message.BinaryBroadcast.Value ? 1 : 0;
            if (_receivedValues[sender].Contains(b)) return;
            _receivedValues[sender].Add(b);
            ++_receivedCount[b];

            if (!_broadcasted[b] && _receivedCount[b] >= _faulty + 1)
            {
                _broadcasted[b] = true;
                _consensusBroadcaster.Broadcast(CreateBValMessage(b));
            }

            if (_receivedCount[b] < 2 * _faulty + 1) return;

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
                    Era = _broadcastId.Era
                },
                BinaryBroadcast = new BinaryBroadcastPayload
                {
                    Agreement = _broadcastId.Agreement,
                    Epoch = _broadcastId.Epoch,
                    Value = value == 1
                }
            };
            return message;
        }
    }
}