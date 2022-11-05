using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Consensus.CommonSubset;
using Lachain.Utility.Serialization;
using Nethereum.RLP;

namespace Lachain.Consensus.ReliableBroadcast
{
    public class ReliableBroadcastId : IProtocolIdentifier
    {
        protected bool Equals(ReliableBroadcastId other)
        {
            return SenderId == other.SenderId && Era == other.Era;
        }

        public bool Equals(IProtocolIdentifier other)
        {
            return Equals((object) other);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ReliableBroadcastId) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(SenderId, Era);
        }

        public int SenderId { get; }

        public long Era { get; }

        public ReliableBroadcastId(int validator, int era)
        {
            SenderId = validator;
            Era = (long) era;
        }

        public override string ToString()
        {
            return $"RBC (E={Era}, A={SenderId})";
        }
        
        public byte[] ToByteArray()
        {
            var list = new List<byte[]>
            {
                SenderId.ToBytes().ToArray(),
                Era.ToBytes().ToArray(),
            };
            return RLP.EncodeList(list.Select(RLP.EncodeElement).ToArray());
        }

        public static ReliableBroadcastId FromByteArray(byte[] bytes)
        {
            var decoded = (RLPCollection) RLP.Decode(bytes.ToArray());
            var senderId = decoded[0].RLPData.AsReadOnlySpan().ToInt32();
            var era = decoded[1].RLPData.AsReadOnlySpan().ToInt64();
            return new ReliableBroadcastId(senderId, (int) era);
        }
    }
}