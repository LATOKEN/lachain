using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Lachain.Logger;
using Lachain.Proto;

namespace Lachain.Consensus.RequestProtocols.Messages
{
    public abstract class MessageResendHandler : IMessageResendHandler
    {
        private static readonly ILogger<MessageResendHandler> Logger = LoggerFactory.GetLoggerForClass<MessageResendHandler>();
        private ConsensusMessage?[][] _sentMessages;
        private readonly RequestType _type;
        private readonly int _validators;
        private readonly int _msgPerValidator;
        public RequestType Type => _type;
        private bool _terminated = false;
        protected MessageResendHandler(RequestType type, int validatorCount, int msgPerValidator)
        {
            _type = type;
            _validators = validatorCount;
            _msgPerValidator = msgPerValidator;
            _sentMessages = new ConsensusMessage?[_validators][];
            for (int i = 0 ; i < _validators ; i++)
            {
                _sentMessages[i] = new ConsensusMessage?[msgPerValidator];
                for (int j = 0; j < msgPerValidator; j++)
                {
                    _sentMessages[i][j] = null;
                }
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Terminate()
        {
            if (_terminated)
                return;
            _terminated = true;
            _sentMessages = new ConsensusMessage?[0][];
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void MessageSent(int validator, ConsensusMessage msg, RequestType type)
        {
            if (_terminated)
                return;
            if (type != _type)
                throw new Exception($"message type {type} routed to MessageResendHandler {_type}");
            HandleSentMessage(validator, msg);
        }

        protected void SaveMessage(int validatorId, int msgId, ConsensusMessage msg)
        {
            if (!(_sentMessages[validatorId][msgId] is null))
                throw new Exception($"Sending duplicate message {msg.ToString()} to validator {validatorId}");
            _sentMessages[validatorId][msgId] = msg;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public List<ConsensusMessage?> HandleRequest(int from, RequestConsensusMessage request, RequestType type)
        {
            if (_terminated)
                return new List<ConsensusMessage?>();

            if (type != _type)
                throw new Exception($"Request message {type} routed to MessageResendHandler {_type}");

            if (from >= _validators)
                throw new Exception($"Got request from validator {from}. But we have total {_validators} validators");

            return HandleRequestMessage(from, request);
        }

        protected ConsensusMessage? GetMessage(int from, int msgId)
        {
            if (from >= _validators)
                throw new Exception($"Got request from validator {from}. But we have total {_validators} validators");
            if (msgId >= _msgPerValidator)
                throw new Exception($"Got request for msgId {msgId}. But we have total {_msgPerValidator} messages per validator");
            return _sentMessages[from][msgId];
        }

        protected abstract List<ConsensusMessage?> HandleRequestMessage(int from, RequestConsensusMessage msg);

        protected abstract void HandleSentMessage(int validator, ConsensusMessage msg);
    }
}