using System;
using System.Collections.Generic;
using System.Linq;

namespace Phorkus.Consensus.CommonCoin
{
    public class CoinId : IProtocolIdentifier
    {
        public CoinId(uint era, uint agreement, uint epoch)
        {
            Era = era;
            Agreement = agreement;
            Epoch = epoch;
        }

        public uint Era { get; }
        public uint Agreement { get; }
        public uint Epoch { get; }

        public IEnumerable<byte> ToByteArray()
        {
            return BitConverter.GetBytes(Era)
                .Concat(BitConverter.GetBytes(Agreement))
                .Concat(BitConverter.GetBytes(Epoch));
        }
    }
}