using System.Collections.Generic;
using System.Linq;
using Lachain.Utility.Serialization;
using Nethereum.RLP;

namespace Lachain.Consensus.RootProtocol
{
    public class RootProtocolId : IProtocolIdentifier
    {

        public long Era { get; }
        
        public RootProtocolId(long era)
        {
            Era = era;
        }

        public override string ToString()
        {
            return $"Root (E={Era})";
        }

        private bool Equals(RootProtocolId other)
        {
            return Era == other.Era;
        }

        public bool Equals(IProtocolIdentifier other)
        {
            return Equals((object?) other);
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((RootProtocolId) obj);
        }

        public override int GetHashCode()
        {
            return Era.GetHashCode();
        }
        public byte[] ToByteArray()
        {
            var list = new List<byte[]>
            {
                Era.ToBytes().ToArray(),
            };
            return RLP.EncodeList(list.Select(RLP.EncodeElement).ToArray());
        }

        public static RootProtocolId FromByteArray(byte[] bytes)
        {
            var decoded = (RLPCollection) RLP.Decode(bytes.ToArray());
            var era = decoded[0].RLPData.AsReadOnlySpan().ToInt64();
            return new RootProtocolId((int)era);
        }
    }
}