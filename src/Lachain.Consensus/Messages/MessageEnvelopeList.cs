using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Utility.Serialization;
using Nethereum.RLP;

namespace Lachain.Consensus.Messages
{
    public class MessageEnvelopeList : IByteSerializable
    {
        public long Era { get; }
        public ICollection<MessageEnvelope> MessageList { get; }
        

        public MessageEnvelopeList(long era)
        {
            this.Era = era;
            this.MessageList = new List<MessageEnvelope>();
        }

        public void AddMessage(MessageEnvelope messageEnvelope)
        {
            MessageList.Add(messageEnvelope);
        }
        
        public byte[] ToByteArray()
        {
            var list = new List<byte[]>();
            
            list.Add(Era.ToBytes().ToArray());
            list.Add(MessageList.Count.ToBytes().ToArray());

            foreach (var message in MessageList)
            {
                list.Add(message.ToByteArray());
            }

            return RLP.EncodeList(list.Select(RLP.EncodeElement).ToArray());
        }

        public static MessageEnvelopeList FromByteArray(byte[] bytes)
        {
            var decoded = (RLPCollection) RLP.Decode(bytes.ToArray());
            var era = decoded[0].RLPData.AsReadOnlySpan().ToInt64();
            var count = decoded[1].RLPData.AsReadOnlySpan().ToInt32();

            var messageEnvelopeList = new MessageEnvelopeList(era);

            for (int i = 0; i < count; i++)
            {
                var envelopeBytes = decoded[2 + i].RLPData;
                messageEnvelopeList.AddMessage(MessageEnvelope.FromByteArray(envelopeBytes));
            }

            return messageEnvelopeList;
        }


        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((MessageEnvelopeList)obj);
        }

        public bool Equals(MessageEnvelopeList other)
        {
            return Era == other.Era && MessageList.Equals(other.MessageList);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Era, MessageList);
        }
    }
}