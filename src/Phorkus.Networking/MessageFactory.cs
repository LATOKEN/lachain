using System;
using Google.Protobuf;
using Phorkus.Crypto;
using Phorkus.Proto;

namespace Phorkus.Networking
{
    public class MessageFactory
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