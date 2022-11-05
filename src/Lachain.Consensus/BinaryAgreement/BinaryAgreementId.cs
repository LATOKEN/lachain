using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Consensus.Messages;
using Lachain.Utility.Serialization;
using Nethereum.RLP;

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

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((BinaryAgreementId) obj);
        }
        
        public bool Equals(IProtocolIdentifier other)
        {
            return Equals((object) other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Era, AssociatedValidatorId);
        }
        
        public override string ToString()
        {
            return $"BA (E={Era}, A={AssociatedValidatorId})";
        }

        public byte[] ToByteArray()
        {
            var list = new List<byte[]>();
            list.Add(Era.ToBytes().ToArray());
            list.Add(AssociatedValidatorId.ToBytes().ToArray());
            return RLP.EncodeList(list.Select(RLP.EncodeElement).ToArray());
        }

        public static BinaryAgreementId FromByteArray(byte[] bytes)
        {
            var decoded = (RLPCollection) RLP.Decode(bytes.ToArray());
            var era = decoded[0].RLPData.AsReadOnlySpan().ToInt64();
            var id = decoded[1].RLPData.AsReadOnlySpan().ToInt64();
            return new BinaryAgreementId(era, id);
        }
    }
}