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
        private readonly Queue<(int,int)> _messageRequests;
        public RequestType Type => _type;
        public MessageRequestHandler(RequestType type, int validatorCount, int msgPerValidator)
        {
            _type = type;
            _validators = validatorCount;
            _msgCount = validatorCount * msgPerValidator;
            _msgPerValidator = msgPerValidator;
            _status = new MessageStatus[_validators][];
            _messageRequests = new Queue<(int, int)>();
            for (int i = 0 ; i < _validators ; i++)
            {
                _status[i] = new MessageStatus[msgPerValidator];
                for (int j = 0; j < msgPerValidator; j++)
                {
                    _status[i][j] = MessageStatus.NotReceived;
                    _messageRequests.Enqueue((i,j));
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

        public void MessageReceived(int validatorId, int msgId)
        {
            if (_status[validatorId][msgId] != MessageStatus.Received)
                _remainingMsges--;
            _status[validatorId][msgId] = MessageStatus.Received;
        }

        public bool IsProtocolComplete()
        {
            return _remainingMsges == _msgCount;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public List<(ConsensusMessage, int)> GetRequests(IProtocolIdentifier protocolId, int requestCount)
        {
            var requests = new List<(ConsensusMessage, int)>();
            if (IsProtocolComplete() || _terminated) return requests;

            if (requestCount > _remainingMsges)
                requestCount = _remainingMsges;
            
            while (requestCount > 0)
            {
                var (validtorId, msgId) = _messageRequests.Dequeue();
                if (_status[validtorId][msgId] == MessageStatus.Received)
                    continue;
                requestCount--;
                if (_status[validtorId][msgId] == MessageStatus.Requested)
                {
                    Logger.LogWarning(
                        $"Requesting consensus msg {_type} with id {msgId} to validator {validtorId} again. Validator not replying."
                    );
                }
                else
                {
                    _status[validtorId][msgId] = MessageStatus.Requested;
                    Logger.LogWarning($"Requesting consensus msg {_type} with id {msgId} to validator {validtorId}.");
                }
                var msg = CreateConsensusMessage(protocolId, msgId);
                if (_type == RequestType.Val)
                    requests.Add((msg, msg.ValMessage.SenderId));
                else
                    requests.Add((msg, validtorId));

                // put this back so we can request it again
                _messageRequests.Enqueue((validtorId, msgId));
            }
            return requests;
        }

        public abstract ConsensusMessage CreateConsensusMessage(IProtocolIdentifier protocolId, int msgId);
        public abstract void HandleReceivedMessage(int from, ConsensusMessage msg);
    }
}