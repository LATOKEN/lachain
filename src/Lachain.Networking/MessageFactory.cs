using System;
using System.Collections.Generic;
using System.Linq;
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
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();

        private readonly EcdsaKeyPair _keyPair;
        private readonly Random _random = new Random();

        public MessageFactory(EcdsaKeyPair keyPair)
        {
            _keyPair = keyPair;
        }

        public ECDSAPublicKey GetPublicKey()
        {
            return _keyPair.PublicKey;
        }

        public NetworkMessage Ack(ulong messageId)
        {
            var ack = new Ack {MessageId = messageId};
            return new NetworkMessage {Ack = ack};
        }

        public NetworkMessage PingReply(ulong timestamp, ulong blockHeight)
        {
            var reply = new PingReply
            {
                Timestamp = timestamp,
                BlockHeight = blockHeight
            };
            return new NetworkMessage {PingReply = reply};
        }

        public NetworkMessage ConsensusMessage(ConsensusMessage message)
        {
            return new NetworkMessage {ConsensusMessage = message};
        }

        public NetworkMessage GetPeersRequest()
        {
            var request = new GetPeersRequest();
            return new NetworkMessage {GetPeersRequest = request};
        }

        public NetworkMessage GetPeersReply(Peer[] peers)
        {
            var reply = new GetPeersReply {Peers = {peers}};
            return new NetworkMessage {GetPeersReply = reply};
        }

        public NetworkMessage SyncPoolRequest(IEnumerable<UInt256> hashes)
        {
            return new NetworkMessage {SyncPoolRequest = new SyncPoolRequest {Hashes = {hashes}}};
        }

        public NetworkMessage SyncPoolReply(IEnumerable<TransactionReceipt> transactions)
        {
            return new NetworkMessage {SyncPoolReply = new SyncPoolReply {Transactions = {transactions}}};
        }

        public NetworkMessage SyncBlocksRequest(ulong fromHeight, ulong toHeight)
        {
            return new NetworkMessage
                {SyncBlocksRequest = new SyncBlocksRequest {FromHeight = fromHeight, ToHeight = toHeight}};
        }

        public MessageBatch MessagesBatch(IEnumerable<NetworkMessage> messages)
        {
            var batch = new MessageBatch
            {
                MessageId = GenerateMessageId(),
                Content = ByteString.CopyFrom(new MessageBatchContent {Messages = {messages}}.ToByteArray()),
                Sender = _keyPair.PublicKey,
            };
            batch.Signature = Crypto.Sign(batch.Content.ToArray(), _keyPair.PrivateKey.Encode(), true).ToSignature();
            return batch;
        }

        public byte[] SignCommunicationHubInit(byte[] hubKey)
        {
            return Crypto.Sign(hubKey, _keyPair.PrivateKey.Encode(), true);
        }

        private ulong GenerateMessageId()
        {
            var buf = new byte[8];
            _random.NextBytes(buf);
            return BitConverter.ToUInt64(buf);
        }
    }
}