using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Utility.Serialization;
using Nethereum.RLP;

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

        //public IEnumerable<byte> ToBytes()
        //{
        //    return Era.ToBytes().Concat(Agreement.ToBytes()).Concat(Epoch.ToBytes());
        //}

        public bool Equals(CoinId other)
        {
            return Era == other.Era && Agreement == other.Agreement && Epoch == other.Epoch;
        }

        public bool Equals(IProtocolIdentifier? other)
        {
            return Equals((object) other!);
        }

        public override bool Equals(object? obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
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

        public static CoinId FromBytes(ReadOnlyMemory<byte> bytes)
        {
            var decoded = (RLPCollection)RLP.Decode(bytes.ToArray());
            var era = decoded[0].RLPData.AsReadOnlySpan().ToInt64();
            var agreement = decoded[1].RLPData.AsReadOnlySpan().ToInt64();
            var epoch = decoded[1].RLPData.AsReadOnlySpan().ToInt64();

            return new CoinId(era, agreement, epoch);
        }
    }
}