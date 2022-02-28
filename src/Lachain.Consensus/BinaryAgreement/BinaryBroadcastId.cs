using Lachain.Utility.Serialization;
using Nethereum.RLP;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lachain.Consensus.BinaryAgreement
{
    public class BinaryBroadcastId : IProtocolIdentifier
    {
        public BinaryBroadcastId(long era, long agreement, long epoch)
        {
            Era = era;
            Agreement = agreement;
            Epoch = epoch;
        }

        public long Era { get; }
        public long Agreement { get; }
        public long Epoch { get; }
        
        protected bool Equals(BinaryBroadcastId other)
        {
            return Era == other.Era && Agreement == other.Agreement && Epoch == other.Epoch;
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
            return Equals((BinaryBroadcastId) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Era.GetHashCode();
                hashCode = (hashCode * 397) ^ Agreement.GetHashCode();
                hashCode = (hashCode * 397) ^ Epoch.GetHashCode();
                return hashCode;
            }
        }
        
        public override string ToString()
        {
            return $"BB (Er={Era}, A={Agreement}, Ep={Epoch})";
        }

        public byte[] ToBytes()
        {
            var bytesArray = new List<byte[]>
            {
                Era.ToBytes().ToArray(),
                Agreement.ToBytes().ToArray(),
                Epoch.ToBytes().ToArray(),
            };

            return RLP.EncodeList(bytesArray.Select(RLP.EncodeElement).ToArray());
        }

        public static BinaryBroadcastId FromBytes(ReadOnlyMemory<byte> bytes)
        {
            var decoded = (RLPCollection)RLP.Decode(bytes.ToArray());
            var era = decoded[0].RLPData.AsReadOnlySpan().ToInt64();
            var agreement = decoded[1].RLPData.AsReadOnlySpan().ToInt64();
            var epoch = decoded[2].RLPData.AsReadOnlySpan().ToInt64();

            return new BinaryBroadcastId(era, agreement, epoch);
        }
    }
}