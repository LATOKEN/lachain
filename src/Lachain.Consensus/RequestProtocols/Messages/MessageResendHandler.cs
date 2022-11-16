using System;
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
        public void MessageReceived(int from, ConsensusMessage msg, RequestType type)
        {
            if (_terminated)
                return;
            if (type != _type)
                throw new Exception($"message type {type} routed to MessageResendHandler {_type}");
            HandleReceivedMessage(from, msg);
        }

        protected void MessageReceived(int validatorId, int msgId, ConsensusMessage msg)
        {
            if (!(_sentMessages[validatorId][msgId] is null))
                throw new Exception($"Sending duplicate message {msg.ToString()} to validator {validatorId}");
            _sentMessages[validatorId][msgId] = msg;
        }

        protected abstract void HandleReceivedMessage(int from, ConsensusMessage msg);
    }
}