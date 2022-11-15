using System;
using System.Collections.Generic;
using Lachain.Logger;
using Lachain.Proto;

namespace Lachain.Consensus.RequestProtocols.Messages
{
    public class MessageResendHandler : IMessageResendHandler
    {
        private static readonly ILogger<MessageResendHandler> Logger = LoggerFactory.GetLoggerForClass<MessageResendHandler>();
        private ConsensusMessage?[][] _sentMessages;
        private readonly RequestType _type;
        private readonly int _validators;
        private readonly int _msgPerValidator;
        public RequestType Type => _type;
        private bool _terminated = false;
        public MessageResendHandler(RequestType type, int validatorCount, int msgPerValidator)
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

        public void Terminate()
        {
            if (_terminated)
                return;
            _terminated = true;
            _sentMessages = new ConsensusMessage?[0][];
        }

        public void MessageReceived(int from, ConsensusMessage msg, RequestType type)
        {
            if (_terminated)
                return;
            if (type != _type)
                throw new Exception($"message type {type} routed to MessageResendHandler {_type}");
            switch (type)
            {
                case RequestType.Aux:
                    HandleAuxMessage(from, msg);
                    break;
                case RequestType.Bval:
                    HandleBValMessage(from, msg);
                    break;
                case RequestType.Coin:
                    HandleCoinMessage(from, msg);
                    break;
                case RequestType.Conf:
                    HandleConfMessage(from, msg);
                    break;
                case RequestType.Decrypted:
                    HandleDecryptedMessage(from, msg);
                    break;
                case RequestType.Echo:
                    HandleEchoMessage(from, msg);
                    break;
                case RequestType.Ready:
                    HandleReadyMessage(from, msg);
                    break;
                case RequestType.SignedHeader:
                    HandleSignedHeaderMessage(from, msg);
                    break;
                case RequestType.Val:
                    HandleValMessage(from, msg);
                    break;
                default:
                    throw new Exception($"Not implemented consensus message {msg.PayloadCase}");
            }
        }

        private void MessageReceived(int validatorId, int msgId, ConsensusMessage msg)
        {
            if (!(_sentMessages[validatorId][msgId] is null))
                throw new Exception($"Sending duplicate message {msg.ToString()} to validator {validatorId}");
            _sentMessages[validatorId][msgId] = msg;
        }

        private void HandleAuxMessage(int from, ConsensusMessage msg)
        {
            MessageReceived(from, 0, msg);
        }

        private void HandleBValMessage(int from, ConsensusMessage msg)
        {
            MessageReceived(from, msg.Bval.Value ? 1 : 0, msg);
        }

        private void HandleConfMessage(int from, ConsensusMessage msg)
        {
            MessageReceived(from, 0, msg);
        }

        private void HandleCoinMessage(int from, ConsensusMessage msg)
        {
            MessageReceived(from, 0, msg);
        }

        private void HandleEchoMessage(int from, ConsensusMessage msg)
        {
            MessageReceived(from, 0, msg);
        }

        private void HandleReadyMessage(int from, ConsensusMessage msg)
        {
            MessageReceived(from, 0, msg);
        }

        private void HandleValMessage(int from, ConsensusMessage msg)
        {
            MessageReceived(from, 0, msg);
        }

        private void HandleDecryptedMessage(int from, ConsensusMessage msg)
        {
            MessageReceived(from, msg.Decrypted.ShareId, msg);
        }

        private void HandleSignedHeaderMessage(int from, ConsensusMessage msg)
        {
            MessageReceived(from, 0, msg);
        }
    }
}