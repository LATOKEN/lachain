using Lachain.Utility.Serialization;
using Nethereum.RLP;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lachain.Consensus.BinaryAgreement
{
    public class BinaryAgreementId : IProtocolIdentifier
    {
        public BinaryAgreementId(long era, long associatedValidatorId)
        {
            Era = era;
            AssociatedValidatorId = associatedValidatorId;
        }

        public long Era { get; }
        public long AssociatedValidatorId { get; }
        
        protected bool Equals(BinaryAgreementId other)
        {
            return Era == other.Era && AssociatedValidatorId == other.AssociatedValidatorId;
        }

        public override bool Equals(object? obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((BinaryAgreementId) obj);
        }
        
        public bool Equals(IProtocolIdentifier? other)
        {
            return Equals((object) other!);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Era, AssociatedValidatorId);
        }
        
        public override string ToString()
        {
            return $"BA (E={Era}, A={AssociatedValidatorId})";
        }

        public byte[] ToBytes()
        {
            var bytesArray = new List<byte[]>
            {
                Era.ToBytes().ToArray(),
                AssociatedValidatorId.ToBytes().ToArray(),
            };

            return RLP.EncodeList(bytesArray.Select(RLP.EncodeElement).ToArray());
        }

        public static BinaryAgreementId FromBytes(ReadOnlyMemory<byte> bytes)
        {
            var decoded = (RLPCollection)RLP.Decode(bytes.ToArray());
            var era = decoded[0].RLPData.AsReadOnlySpan().ToInt64();
            var associatedValidatorId = decoded[1].RLPData.AsReadOnlySpan().ToInt64();

            return new BinaryAgreementId(era, associatedValidatorId);
        }
    }
}