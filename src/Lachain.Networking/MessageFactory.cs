using System;
using System.Collections.Generic;
using Google.Protobuf;
using Lachain.Crypto;
using Lachain.Crypto.ECDSA;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Networking
{
    public class MessageFactory : IMessageFactory
    {
        private static readonly ILogger<MessageFactory> Logger = LoggerFactory.GetLoggerForClass<MessageFactory>();

        private readonly EcdsaKeyPair _keyPair;
        private readonly ICrypto _crypto = CryptoProvider.GetCrypto();
        private readonly Random _random = new Random();

        public MessageFactory(EcdsaKeyPair keyPair)
        {
            _keyPair = keyPair;
        }

        public NetworkMessage Ack(ulong messageId)
        {
            var ack = new Ack {MessageId = messageId};
            var sig = _SignMessage(ack);
            return new NetworkMessage
            {
                Ack = ack,
                Signature = sig
            };
        }

        public NetworkMessage HandshakeRequest(Node node)
        {
            var request = new HandshakeRequest
            {
                Node = node
            };
            var sig = _SignMessage(request);
            return new NetworkMessage
            {
                MessageId = GenerateMessageId(),
                HandshakeRequest = request,
                Signature = sig
            };
        }

        public NetworkMessage HandshakeReply(Node node, int port)
        {
            var reply = new HandshakeReply
            {
                Node = node,
                Port = (uint) port
            };
            var sig = _SignMessage(reply);
            return new NetworkMessage
            {
                MessageId = GenerateMessageId(),
                HandshakeReply = reply,
                Signature = sig
            };
        }

        public NetworkMessage PingRequest(ulong timestamp, ulong blockHeight)
        {
            var request = new PingRequest
            {
                Timestamp = timestamp,
                BlockHeight = blockHeight
            };
            var sig = _SignMessage(request);
            return new NetworkMessage
            {
                MessageId = GenerateMessageId(),
                PingRequest = request,
                Signature = sig
            };
        }

        public NetworkMessage PingReply(ulong timestamp, ulong blockHeight)
        {
            var reply = new PingReply
            {
                Timestamp = timestamp,
                BlockHeight = blockHeight
            };
            var sig = _SignMessage(reply);
            return new NetworkMessage
            {
                MessageId = GenerateMessageId(),
                PingReply = reply,
                Signature = sig
            };
        }

        public NetworkMessage ConsensusMessage(ConsensusMessage message)
        {
            return new NetworkMessage
            {
                MessageId = GenerateMessageId(),
                ConsensusMessage = message,
                Signature = _SignMessage(message)
            };
        }

        public NetworkMessage GetBlocksByHashesRequest(IEnumerable<UInt256> blockHashes)
        {
            var request = new GetBlocksByHashesRequest
            {
                BlockHashes = {blockHashes}
            };
            var sig = _SignMessage(request);
            return new NetworkMessage
            {
                MessageId = GenerateMessageId(),
                GetBlocksByHashesRequest = request,
                Signature = sig
            };
        }

        public NetworkMessage GetBlocksByHashesReply(IEnumerable<Block> blocks)
        {
            var reply = new GetBlocksByHashesReply
            {
                Blocks = {blocks}
            };
            var sig = _SignMessage(reply);
            return new NetworkMessage
            {
                MessageId = GenerateMessageId(),
                GetBlocksByHashesReply = reply,
                Signature = sig
            };
        }

        public NetworkMessage GetBlocksByHeightRangeRequest(ulong fromHeight, ulong toHeight)
        {
            var request = new GetBlocksByHeightRangeRequest
            {
                FromHeight = fromHeight,
                ToHeight = toHeight
            };
            var sig = _SignMessage(request);
            return new NetworkMessage
            {
                MessageId = GenerateMessageId(),
                GetBlocksByHeightRangeRequest = request,
                Signature = sig
            };
        }

        public NetworkMessage GetBlocksByHeightRangeReply(IEnumerable<UInt256> blockHashes)
        {
            var reply = new GetBlocksByHeightRangeReply
            {
                BlockHashes = {blockHashes}
            };
            var sig = _SignMessage(reply);
            return new NetworkMessage
            {
                MessageId = GenerateMessageId(),
                GetBlocksByHeightRangeReply = reply,
                Signature = sig
            };
        }

        public NetworkMessage GetTransactionsByHashesRequest(IEnumerable<UInt256> transactionHashes)
        {
            var request = new GetTransactionsByHashesRequest
            {
                TransactionHashes = {transactionHashes}
            };
            var sig = _SignMessage(request);
            return new NetworkMessage
            {
                MessageId = GenerateMessageId(),
                GetTransactionsByHashesRequest = request,
                Signature = sig
            };
        }

        public NetworkMessage GetTransactionsByHashesReply(IEnumerable<TransactionReceipt> transactions)
        {
            var reply = new GetTransactionsByHashesReply
            {
                Transactions = {transactions}
            };
            var sig = _SignMessage(reply);
            return new NetworkMessage
            {
                MessageId = GenerateMessageId(),
                GetTransactionsByHashesReply = reply,
                Signature = sig
            };
        }

        private ulong GenerateMessageId()
        {
            var buf = new byte[8];
            _random.NextBytes(buf);
            return BitConverter.ToUInt64(buf);
        }

        private Signature _SignMessage(IMessage message)
        {
            return _crypto.Sign(message.ToByteArray(), _keyPair.PrivateKey.Encode()).ToSignature();
        }
    }
}