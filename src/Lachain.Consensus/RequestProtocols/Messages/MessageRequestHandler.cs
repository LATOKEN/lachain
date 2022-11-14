using System;
using System.Collections.Generic;
using Lachain.Consensus.BinaryAgreement;
using Lachain.Consensus.CommonCoin;
using Lachain.Consensus.HoneyBadger;
using Lachain.Consensus.ReliableBroadcast;
using Lachain.Consensus.RootProtocol;
using Lachain.Logger;
using Lachain.Proto;

namespace Lachain.Consensus.RequestProtocols.Messages
{
    public class MessageRequestHandler : IMessageRequestHandler
    {
        private static readonly ILogger<MessageRequestHandler> Logger = LoggerFactory.GetLoggerForClass<MessageRequestHandler>();
        private MessageStatus[][] _status;
        private readonly int _msgCount;
        private readonly RequestType _type;
        private readonly int _validators;
        private readonly int _msgPerValidator;
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

        public void Terminate()
        {
            _messageRequests.Clear();
            _status = new MessageStatus[0][];
        }

        public void MessageReceived(int from, ConsensusMessage msg)
        {
            var type = GetRequestTypeForMessageType(msg);
            if (type != _type)
                throw new Exception($"message type {type} routed to message handler {_type}");
            switch (type)
            {
                case RequestType.Aux:
                    HandleAuxMessage(from, msg.Aux);
                    break;
                case RequestType.Bval:
                    HandleBValMessage(from, msg.Bval);
                    break;
                case RequestType.Coin:
                    HandleCoinMessage(from, msg.Coin);
                    break;
                case RequestType.Conf:
                    HandleConfMessage(from, msg.Conf);
                    break;
                case RequestType.Decrypted:
                    HandleDecryptedMessage(from, msg.Decrypted);
                    break;
                case RequestType.Echo:
                    HandleEchoMessage(from, msg.EchoMessage);
                    break;
                case RequestType.Ready:
                    HandleReadyMessage(from, msg.ReadyMessage);
                    break;
                case RequestType.SignedHeader:
                    HandleSignedHeaderMessage(from, msg.SignedHeaderMessage);
                    break;
                case RequestType.Val:
                    HandleValMessage(from, msg.ValMessage);
                    break;
                default:
                    throw new Exception($"Not implemented consensus message {msg.PayloadCase}");
            }
        }

        private void MessageReceived(int validatorId, int msgId)
        {
            if (_status[validatorId][msgId] != MessageStatus.Received)
                _remainingMsges--;
            _status[validatorId][msgId] = MessageStatus.Received;
        }

        private void HandleAuxMessage(int from, AuxMessage _)
        {
            MessageReceived(from, 0);
        }

        private void HandleBValMessage(int from, BValMessage msg)
        {
            MessageReceived(from, msg.Value ? 1 : 0);
        }

        private void HandleConfMessage(int from, ConfMessage _)
        {
            MessageReceived(from, 0);
        }

        private void HandleCoinMessage(int from, CommonCoinMessage _)
        {
            MessageReceived(from, 0);
        }

        private void HandleEchoMessage(int from, ECHOMessage _)
        {
            MessageReceived(from, 0);
        }

        private void HandleReadyMessage(int from, ReadyMessage _)
        {
            MessageReceived(from, 0);
        }

        private void HandleValMessage(int _from, ValMessage _)
        {
            MessageReceived(0, 0);
        }

        private void HandleDecryptedMessage(int from, TPKEPartiallyDecryptedShareMessage msg)
        {
            MessageReceived(from, msg.ShareId);
        }

        private void HandleSignedHeaderMessage(int from, SignedHeaderMessage _)
        {
            MessageReceived(from, 0);
        }

        public bool IsProtocolComplete()
        {
            return _remainingMsges == _msgCount;
        }

        public List<(ConsensusMessage, int)> GetRequests(IProtocolIdentifier protocolId, int requestCount)
        {
            var requests = new List<(ConsensusMessage, int)>();
            if (IsProtocolComplete()) return requests;

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

        private ConsensusMessage CreateConsensusMessage(IProtocolIdentifier protocolId, int msgId)
        {
            var wrongProtocolWarning = $"wrong protcolId {protocolId} for request type {_type}";
            switch (_type)
            {
                case RequestType.Aux:
                    return CreateAuxRequest(protocolId as BinaryBroadcastId ?? throw new Exception(wrongProtocolWarning));
                case RequestType.Bval:
                    return CreateBValRequest(protocolId as BinaryBroadcastId ?? throw new Exception(wrongProtocolWarning));
                case RequestType.Conf:
                    return CreateConfRequest(protocolId as BinaryBroadcastId ?? throw new Exception(wrongProtocolWarning));
                case RequestType.Coin:
                    return CreateCoinRequest(protocolId as CoinId ?? throw new Exception(wrongProtocolWarning));
                case RequestType.Decrypted:
                    return CreateDecryptedRequest(protocolId as HoneyBadgerId ?? throw new Exception(wrongProtocolWarning), msgId);
                case RequestType.Echo:
                    return CreateEchoRequest(protocolId as ReliableBroadcastId ?? throw new Exception(wrongProtocolWarning));
                case RequestType.Ready:
                    return CreateReadyRequest(protocolId as ReliableBroadcastId ?? throw new Exception(wrongProtocolWarning));
                case RequestType.Val:
                    return CreateValRequest(protocolId as ReliableBroadcastId ?? throw new Exception(wrongProtocolWarning));
                case RequestType.SignedHeader:
                    return CreateSignedHeaderRequest(protocolId as RootProtocolId ?? throw new Exception(wrongProtocolWarning));
                default:
                    throw new Exception($"Not implemented request type {_type}");
            }
        }

        private ConsensusMessage CreateAuxRequest(BinaryBroadcastId id)
        {
            if (_type != RequestType.Aux)
                throw new Exception($"Aux request routed to {_type} message handler");
            var auxRequest = new RequestAuxMessage
            {
                Agreement = (int) id.Agreement,
                Epoch = (int) id.Epoch
            };
            return new ConsensusMessage
            {
                RequestConsensus = new RequestConsensusMessage
                {
                    RequestAux = auxRequest
                }
            };
        }

        private ConsensusMessage CreateBValRequest(BinaryBroadcastId id)
        {
            if (_type != RequestType.Bval)
                throw new Exception($"BVal request routed to {_type} message handler");
            var bvalRequest = new RequestBValMessage
            {
                Agreement = (int) id.Agreement,
                Epoch = (int) id.Epoch
            };
            return new ConsensusMessage
            {
                RequestConsensus = new RequestConsensusMessage
                {
                    RequestBval = bvalRequest
                }
            };
        }

        private ConsensusMessage CreateConfRequest(BinaryBroadcastId id)
        {
            if (_type != RequestType.Conf)
                throw new Exception($"Conf request routed to {_type} message handler");
            var confRequest = new RequestConfMessage
            {
                Agreement = (int) id.Agreement,
                Epoch = (int) id.Epoch
            };
            return new ConsensusMessage
            {
                RequestConsensus = new RequestConsensusMessage
                {
                    RequestConf = confRequest
                }
            };
        }

        private ConsensusMessage CreateCoinRequest(CoinId id)
        {
            if (_type != RequestType.Coin)
                throw new Exception($"Coin request routed to {_type} message handler");
            var coinRequest = new RequestCommonCoinMessage
            {
                Agreement = (int) id.Agreement,
                Epoch = (int) id.Epoch
            };
            return new ConsensusMessage
            {
                RequestConsensus = new RequestConsensusMessage
                {
                    RequestCoin = coinRequest
                }
            };
        }

        private ConsensusMessage CreateValRequest(ReliableBroadcastId id)
        {
            if (_type != RequestType.Val)
                throw new Exception($"Val request routed to {_type} message handler");
            var valRequest = new RequestValMessage
            {
                SenderId = id.SenderId
            };
            return new ConsensusMessage
            {
                RequestConsensus = new RequestConsensusMessage
                {
                    RequestVal = valRequest
                }
            };
        }

        private ConsensusMessage CreateEchoRequest(ReliableBroadcastId id)
        {
            if (_type != RequestType.Echo)
                throw new Exception($"Echo request routed to {_type} message handler");
            var echoRequest = new RequestECHOMessage
            {
                SenderId = id.SenderId
            };
            return new ConsensusMessage
            {
                RequestConsensus = new RequestConsensusMessage
                {
                    RequestEcho = echoRequest
                }
            };
        }

        private ConsensusMessage CreateReadyRequest(ReliableBroadcastId id)
        {
            if (_type != RequestType.Ready)
                throw new Exception($"Ready request routed to {_type} message handler");
            var readyRequest = new RequestReadyMessage
            {
                SenderId = id.SenderId
            };
            return new ConsensusMessage
            {
                RequestConsensus = new RequestConsensusMessage
                {
                    RequestReady = readyRequest
                }
            };
        }

        private ConsensusMessage CreateDecryptedRequest(HoneyBadgerId _, int shareId)
        {
            if (_type != RequestType.Decrypted)
                throw new Exception($"Decrypted request routed to {_type} message handler");
            var decryptedRequest = new RequestTPKEPartiallyDecryptedShareMessage
            {
                ShareId = shareId
            };
            return new ConsensusMessage
            {
                RequestConsensus = new RequestConsensusMessage
                {
                    RequestDecrypted = decryptedRequest
                }
            };
        }

        private ConsensusMessage CreateSignedHeaderRequest(RootProtocolId _)
        {
            if (_type != RequestType.SignedHeader)
                throw new Exception($"Signed header request routed to {_type} message handler");
            var headerRequest = new RequestSignedHeaderMessage();
            return new ConsensusMessage
            {
                RequestConsensus = new RequestConsensusMessage
                {
                    RequestSignedHeader = headerRequest
                }
            };
        }

        public static RequestType GetRequestTypeForMessageType(ConsensusMessage msg)
        {
            switch (msg.PayloadCase)
            {
                case ConsensusMessage.PayloadOneofCase.Aux:
                    return RequestType.Aux;;
                case ConsensusMessage.PayloadOneofCase.Bval:
                    return RequestType.Bval;
                case ConsensusMessage.PayloadOneofCase.Coin:
                    return RequestType.Coin;
                case ConsensusMessage.PayloadOneofCase.Conf:
                    return RequestType.Conf;
                case ConsensusMessage.PayloadOneofCase.Decrypted:
                    return RequestType.Decrypted;
                case ConsensusMessage.PayloadOneofCase.EchoMessage:
                    return RequestType.Echo;
                case ConsensusMessage.PayloadOneofCase.ReadyMessage:
                    return RequestType.Ready;
                case ConsensusMessage.PayloadOneofCase.SignedHeaderMessage:
                    return RequestType.SignedHeader;
                case ConsensusMessage.PayloadOneofCase.ValMessage:
                    return RequestType.Val;
                default:
                    throw new Exception($"Not implemented consensus message {msg.PayloadCase}");
            }
        }
    }
}