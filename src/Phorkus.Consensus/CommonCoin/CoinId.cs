using System;
using System.Collections.Generic;
using System.Linq;

namespace Phorkus.Consensus.CommonCoin
{
    public class CoinId : IProtocolIdentifier
    {
        public CoinId(ulong era, ulong agreement, ulong epoch)
        {
            Era = era;
            Agreement = agreement;
            Epoch = epoch;
        }

        public ulong Era { get; }
        public ulong Agreement { get; }
        public ulong Epoch { get; }

        public IEnumerable<byte> ToByteArray()
        {
            return BitConverter.GetBytes(Era)
                .Concat(BitConverter.GetBytes(Agreement))
                .Concat(BitConverter.GetBytes(Epoch));
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
            unchecked
            {
                var hashCode = Era.GetHashCode();
                hashCode = (hashCode * 397) ^ Agreement.GetHashCode();
                hashCode = (hashCode * 397) ^ Epoch.GetHashCode();
                return hashCode;
            }
        }
    }
}