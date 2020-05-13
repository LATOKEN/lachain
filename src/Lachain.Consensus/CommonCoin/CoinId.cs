using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Utility.Serialization;

namespace Lachain.Consensus.CommonCoin
{
    public class CoinId : IProtocolIdentifier
    {
        public CoinId(long era, long agreement, long epoch)
        {
            Era = era;
            Agreement = agreement;
            Epoch = epoch;
        }

        public long Era { get; }
        public long Agreement { get; }
        public long Epoch { get; }

        public IEnumerable<byte> ToBytes()
        {
            return Era.ToBytes().Concat(Agreement.ToBytes()).Concat(Epoch.ToBytes());
        }

        public bool Equals(CoinId other)
        {
            return Era == other.Era && Agreement == other.Agreement && Epoch == other.Epoch;
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
            return Equals((CoinId) obj);
        }

        public override string ToString()
        {
            return $"CC (Er={Era}, A={Agreement}, Ep={Epoch})";
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Era, Agreement, Epoch);
        }
    }
}