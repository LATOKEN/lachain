using System;
using Lachain.Proto;

namespace Lachain.Consensus.RequestProtocols.Messages
{
    public static class MessageUtils
    {
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