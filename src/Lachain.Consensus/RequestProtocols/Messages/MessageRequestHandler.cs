using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Lachain.Logger;
using Lachain.Proto;

namespace Lachain.Consensus.RequestProtocols.Messages
{
    public abstract class MessageRequestHandler : IMessageRequestHandler
    {
        private static readonly ILogger<MessageRequestHandler> Logger = LoggerFactory.GetLoggerForClass<MessageRequestHandler>();
        private MessageStatus[][] _status;
        private readonly int _msgCount;
        private readonly RequestType _type;
        private readonly int _validators;
        private readonly int _msgPerValidator;
        private bool _terminated = false;
        private int _remainingMsges;
        private readonly Queue<(int validatorId, int msgId, ulong requestTime)> _messageRequests;
        public RequestType Type => _type;
        public int RemainingMsgCount => _remainingMsges;
        protected MessageRequestHandler(RequestType type, int validatorCount, int msgPerValidator)
        {
            _type = type;
            _validators = validatorCount;
            _msgCount = validatorCount * msgPerValidator;
            _msgPerValidator = msgPerValidator;
            _status = new MessageStatus[_validators][];
            _messageRequests = new Queue<(int, int, ulong)>();
            for (int i = 0 ; i < _validators ; i++)
            {
                _status[i] = new MessageStatus[msgPerValidator];
                for (int j = 0; j < msgPerValidator; j++)
                {
                    _status[i][j] = MessageStatus.NotReceived;
                    _messageRequests.Enqueue((i, j, 0));
                }
            }
            _remainingMsges = _msgCount;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Terminate()
        {
            if (_terminated)
                return;
            _terminated = true;
            _messageRequests.Clear();
            _status = new MessageStatus[0][];
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void MessageReceived(int from, ConsensusMessage msg, RequestType type)
        {
            if (_terminated)
                return;
            if (type != _type)
                throw new Exception($"message type {type} routed to message handler {_type}");
            HandleReceivedMessage(from, msg);
        }

        protected void MessageReceived(int validatorId, int msgId)
        {
            if (_status[validatorId][msgId] != MessageStatus.Received)
                _remainingMsges--;
            _status[validatorId][msgId] = MessageStatus.Received;
        }

        public bool IsProtocolComplete()
        {
            return _remainingMsges == 0;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public Tuple<int, int, ulong, MessageStatus>? Peek()
        {
            while (_messageRequests.Count > 0)
            {
                var (validatorId, msgId, requestTime) = _messageRequests.Peek();
                if (_status[validatorId][msgId] == MessageStatus.Received)
                {
                    _messageRequests.Dequeue();
                }
                else
                {
                    return Tuple.Create(validatorId, msgId, requestTime, _status[validatorId][msgId]);
                }
            }
            return null;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Dequeue()
        {
            _messageRequests.Dequeue();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Enqueue(int validatorId, int msgId, ulong requestTime)
        {
            _messageRequests.Enqueue((validatorId, msgId, requestTime));
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void MessageRequested(int validatorId, int msgId)
        {
            _status[validatorId][msgId] = MessageStatus.Requested;
        }

        public abstract ConsensusMessage CreateConsensusRequestMessage(IProtocolIdentifier protocolId, int msgId);
        protected abstract void HandleReceivedMessage(int from, ConsensusMessage msg);
    }
}