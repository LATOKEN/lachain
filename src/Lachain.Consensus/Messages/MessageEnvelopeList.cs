using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Utility.Serialization;
using Nethereum.RLP;

namespace Lachain.Consensus.Messages
{
    public class MessageEnvelopeList : IByteSerializable
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
        
        public byte[] ToByteArray()
        {
            var list = new List<byte[]>();
            
            list.Add(era.ToBytes().ToArray());
            list.Add(messageList.Count.ToBytes().ToArray());

            foreach (var message in messageList)
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
                messageEnvelopeList.addMessage(MessageEnvelope.FromByteArray(envelopeBytes));
            }

            return messageEnvelopeList;
        }
    }
}