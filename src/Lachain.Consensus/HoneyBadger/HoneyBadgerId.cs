using System.Collections.Generic;
using System.Linq;
using Lachain.Consensus.CommonSubset;
using Lachain.Utility.Serialization;
using Nethereum.RLP;

namespace Lachain.Consensus.HoneyBadger

{
    public class HoneyBadgerId : IProtocolIdentifier
    {
        public HoneyBadgerId(long era)
        {
            Era = era;
        }
        
        protected bool Equals(HoneyBadgerId other)
        {
            return Era == other.Era;
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
            return Equals((HoneyBadgerId) obj);
        }

        public override int GetHashCode()
        {
            return Era.GetHashCode();
        }

        public long Era { get; }

        public override string ToString()
        {
            return $"HB (Er={Era})";
        }
        
        public byte[] ToByteArray()
        {
            var list = new List<byte[]>
            {
                Era.ToBytes().ToArray(),
            };
            return RLP.EncodeList(list.Select(RLP.EncodeElement).ToArray());
        }

        public static HoneyBadgerId FromByteArray(byte[] bytes)
        {
            var decoded = (RLPCollection) RLP.Decode(bytes.ToArray());
            var era = decoded[0].RLPData.AsReadOnlySpan().ToInt64();
            return new HoneyBadgerId(era);
        }
    }
}