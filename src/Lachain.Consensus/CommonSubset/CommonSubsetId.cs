using Lachain.Consensus.HoneyBadger;
using Lachain.Utility.Serialization;
using Nethereum.RLP;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lachain.Consensus.CommonSubset
{
    public class CommonSubsetId : IProtocolIdentifier
    {
        public CommonSubsetId(HoneyBadgerId honeyBadgerId)
        {
            Era = honeyBadgerId.Era;
        }

        public CommonSubsetId(int era)
        {
            Era = (long) era;
        }

        public long Era { get; }

        public override string ToString()
        {
            return $"ACS ({nameof(Era)}={Era})";
        }

        public bool Equals(IProtocolIdentifier other)
        {
            return Equals((object) other);
        }

        protected bool Equals(CommonSubsetId other)
        {
            return Era == other.Era;
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((CommonSubsetId) obj);
        }

        public override int GetHashCode()
        {
            return Era.GetHashCode();
        }

        public byte[] ToBytes()
        {
            var bytesArray = new List<byte[]>
            {
                Era.ToBytes().ToArray()
            };

            return RLP.EncodeList(bytesArray.Select(RLP.EncodeElement).ToArray());
        }

        public static CommonSubsetId FromBytes(ReadOnlyMemory<byte> bytes)
        {
            var decoded = (RLPCollection)RLP.Decode(bytes.ToArray());
            var era = decoded[0].RLPData.AsReadOnlySpan().ToInt64();

            return new CommonSubsetId(era);
        }
    }
}