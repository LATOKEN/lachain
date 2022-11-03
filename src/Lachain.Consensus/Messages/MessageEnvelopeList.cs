using System;
using System.Collections.Generic;

namespace Lachain.Consensus.Messages
{
    public class MessageEnvelopeList
    {
        public long era { get; }
        public ICollection<MessageEnvelope> messageList { get; }
        

        public MessageEnvelopeList(long era)
        {
            this.era = era;
            this.messageList = new List<MessageEnvelope>();
        }

        public void addMessage(MessageEnvelope messageEnvelope)
        {
            messageList.Add(messageEnvelope);
        }
        
        public byte[] ToBytes()
        {
            throw new NotImplementedException();
            // var a = new List<byte[]>
            // {
            //     era.ToBytes().ToArray()
            // };
            // foreach (var message in _messageList)
            // {
            //     a.Add(message.ToBytes());
            // }
            //
            // return RLP.EncodeList(a.Select(RLP.EncodeElement).ToArray());
        }

        public static MessageEnvelopeList FromBytes(byte[] bytes)
        {
            throw new NotImplementedException();
        }
    }
}