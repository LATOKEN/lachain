using Lachain.Utility.Serialization;
using Nethereum.RLP;
using System;
using System.Collections.Generic;
using System.Linq;

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
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ReliableBroadcastId) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(SenderId, Era);
        }

        public int SenderId { get; }

        public long Era { get; }

        public ReliableBroadcastId(int validator, long era)
        {
            SenderId = validator;
            Era = era;
        }

        public override string ToString()
        {
            return $"RBC (E={Era}, A={SenderId})";
        }

        public byte[] ToBytes()
        {
            var bytesArray = new List<byte[]>
            {
                SenderId.ToBytes().ToArray(),
                Era.ToBytes().ToArray(),
            };

            return RLP.EncodeList(bytesArray.Select(RLP.EncodeElement).ToArray());
        }

        public static ReliableBroadcastId FromBytes(ReadOnlyMemory<byte> bytes)
        {
            var decoded = (RLPCollection)RLP.Decode(bytes.ToArray());
            var senderId = decoded[0].RLPData.AsReadOnlySpan().ToInt32();
            var era = decoded[1].RLPData.AsReadOnlySpan().ToInt64();

            return new ReliableBroadcastId(senderId, era);
        }
    }
}