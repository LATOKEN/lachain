using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Lachain.Crypto;
using Lachain.Crypto.ECDSA;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Utility.Utils;
using Nethereum.RLP;

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
        
        public NetworkMessage PingRequest(ulong timestamp, ulong blockHeight)
        {
            var request = new PingRequest
            {
                Timestamp = timestamp,
                BlockHeight = blockHeight
            };
            return new NetworkMessage {PingRequest = request};
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

        public NetworkMessage GetBlocksByHashesRequest(IEnumerable<UInt256> blockHashes)
        {
            var request = new GetBlocksByHashesRequest {BlockHashes = {blockHashes}};
            return new NetworkMessage {GetBlocksByHashesRequest = request};
        }

        public NetworkMessage GetPeersRequest()
        {
            var request = new GetPeersRequest();
            return new NetworkMessage
            {
                GetPeersRequest = request,
            };
        }

        public NetworkMessage PeerJoinRequest(Peer peer)
        {
            var request = new PeerJoinRequest
            {
                Peer = peer,
            };
            return new NetworkMessage
            {
                PeerJoinRequest = request,
            };
        }

        public NetworkMessage GetPeersReply(Peer[] peers, ECDSAPublicKey[] publicKeys)
        {
            var reply = new GetPeersReply
            {
                Peers = {peers},
                PublicKeys = {publicKeys},
            };
            return new NetworkMessage
            {
                GetPeersReply = reply,
            };
        }

        public NetworkMessage GetBlocksByHashesReply(IEnumerable<Block> blocks)
        {
            var reply = new GetBlocksByHashesReply {Blocks = {blocks}};
            return new NetworkMessage {GetBlocksByHashesReply = reply};
        }

        public NetworkMessage GetBlocksByHeightRangeRequest(ulong fromHeight, ulong toHeight)
        {
            var request = new GetBlocksByHeightRangeRequest
            {
                FromHeight = fromHeight,
                ToHeight = toHeight
            };
            return new NetworkMessage {GetBlocksByHeightRangeRequest = request};
        }

        public NetworkMessage GetBlocksByHeightRangeReply(IEnumerable<UInt256> blockHashes)
        {
            var reply = new GetBlocksByHeightRangeReply {BlockHashes = {blockHashes}};
            return new NetworkMessage {GetBlocksByHeightRangeReply = reply};
        }

        public NetworkMessage GetTransactionsByHashesRequest(IEnumerable<UInt256> transactionHashes)
        {
            var request = new GetTransactionsByHashesRequest {TransactionHashes = {transactionHashes}};
            return new NetworkMessage {GetTransactionsByHashesRequest = request};
        }

        public NetworkMessage GetTransactionsByHashesReply(IEnumerable<TransactionReceipt> transactions)
        {
            var reply = new GetTransactionsByHashesReply {Transactions = {transactions}};
            return new NetworkMessage {GetTransactionsByHashesReply = reply};
        }

        public MessageBatch MessagesBatch(IEnumerable<NetworkMessage> messages)
        {
            var batch = new MessageBatch
            {
                MessageId = GenerateMessageId(),
                Content = ByteString.CopyFrom(new MessageBatchContent {Messages = {messages}}.ToByteArray()),
                Sender = _keyPair.PublicKey,
            };
            batch.Signature = Crypto.Sign(batch.Content.ToArray(), _keyPair.PrivateKey.Encode()).ToSignature();
            return batch;
        }

        public Signature SignCommunicationHubSend(ECDSAPublicKey to, byte[] payload)
        {
            return Crypto.SignHashed(
                to.EncodeCompressed().Concat(payload).ToArray().KeccakBytes(),
                _keyPair.PrivateKey.Encode()
            ).ToSignature();
        }

        public Signature SignCommunicationHubReceive(ulong timestamp)
        {
            var tsBytes = BitConverter.GetBytes(timestamp);
            if (BitConverter.IsLittleEndian) tsBytes = tsBytes.Reverse().ToArray();
            return Crypto.SignHashed(
                _keyPair.PublicKey.EncodeCompressed().Concat(tsBytes).ToArray().KeccakBytes(),
                _keyPair.PrivateKey.Encode()
            ).ToSignature();
        }

        public byte[] SignCommunicationHubInit(byte[] nodeKey, byte[] hubKey)
        {
            var toSign = RLP.EncodeList(
                RLP.EncodeElement(nodeKey),
                RLP.EncodeElement(hubKey)
            );
            return Crypto.Sign(toSign, _keyPair.PrivateKey.Encode());
        }

        private ulong GenerateMessageId()
        {
            var buf = new byte[8];
            _random.NextBytes(buf);
            return BitConverter.ToUInt64(buf);
        }
    }
}