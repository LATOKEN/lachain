﻿using System;
using System.Collections.Generic;
using Google.Protobuf;
using Phorkus.Crypto;
using Phorkus.Proto;

namespace Phorkus.Networking
{
    public class MessageFactory : IMessageFactory
    {
        private readonly KeyPair _keyPair;
        private readonly ICrypto _crypto;
        
        public MessageFactory(KeyPair keyPair, ICrypto crypto)
        {
            _keyPair = keyPair;
            _crypto = crypto;
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
                HandshakeRequest = request,
                Signature = sig
            };
        }

        public NetworkMessage HandshakeReply(Node node)
        {
            var reply = new HandshakeReply
            {
                Node = node
            };
            var sig = _SignMessage(reply);
            return new NetworkMessage
            {
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
                PingReply = reply,
                Signature = sig
            };
        }

        public NetworkMessage ConsensusMessage(ConsensusMessage message)
        {
            return new NetworkMessage
            {
                ConsensusMessage = message,
                Signature = _SignMessage(message)
            };
        }

        public NetworkMessage GetBlocksByHashesRequest(IEnumerable<UInt256> blockHashes)
        {
            var request = new GetBlocksByHashesRequest
            {
                BlockHashes = { blockHashes }
            };
            var sig = _SignMessage(request);
            return new NetworkMessage
            {
                GetBlocksByHashesRequest = request,
                Signature = sig
            };
        }

        public NetworkMessage GetBlocksByHashesReply(IEnumerable<Block> blocks)
        {
            var reply = new GetBlocksByHashesReply
            {
                Blocks = { blocks }
            };
            var sig = _SignMessage(reply);
            return new NetworkMessage
            {
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
                GetBlocksByHeightRangeRequest = request,
                Signature = sig
            };
        }

        public NetworkMessage GetBlocksByHeightRangeReply(IEnumerable<UInt256> blockHashes)
        {
            var reply = new GetBlocksByHeightRangeReply
            {
                BlockHashes = { blockHashes }
            };
            var sig = _SignMessage(reply);
            return new NetworkMessage
            {
                GetBlocksByHeightRangeReply = reply,
                Signature = sig
            };
        }

        public NetworkMessage GetTransactionsByHashesRequest(IEnumerable<UInt256> transactionHashes)
        {
            var request = new GetTransactionsByHashesRequest
            {
                TransactionHashes = { transactionHashes }
            };
            var sig = _SignMessage(request);
            return new NetworkMessage
            {
                GetTransactionsByHashesRequest = request,
                Signature = sig
            };
        }

        public NetworkMessage GetTransactionsByHashesReply(IEnumerable<SignedTransaction> transactions)
        {
            var reply = new GetTransactionsByHashesReply
            {
                Transactions = { transactions }
            };
            var sig = _SignMessage(reply);
            return new NetworkMessage
            {
                GetTransactionsByHashesReply = reply,
                Signature = sig
            };
        }

        public NetworkMessage ThresholdRequest(byte[] message)
        {
            throw new NotImplementedException();
        }

        public NetworkMessage ChangeViewRequest()
        {
            throw new NotImplementedException();
        }

        public NetworkMessage BlockPrepareRequest()
        {
            throw new NotImplementedException();
        }

        public NetworkMessage BlockPrepareReply()
        {
            throw new NotImplementedException();
        }

        private Signature _SignMessage(IMessage message)
        {
            var rawSig = _crypto.Sign(message.ToByteArray(), _keyPair.PrivateKey.Buffer.ToByteArray());
            if (rawSig.Length != 65)
                throw new ArgumentOutOfRangeException(nameof(rawSig));
            return new Signature
            {
                Buffer = ByteString.CopyFrom(rawSig)
            };
        }
    }
}