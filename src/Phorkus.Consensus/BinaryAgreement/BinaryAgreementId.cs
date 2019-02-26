using System;
using System.Collections.Generic;
using System.Linq;

namespace Phorkus.Consensus.BinaryAgreement
{
    public class BinaryAgreementId : IProtocolIdentifier
    {
        public BinaryAgreementId(uint era, uint agreement)
        {
            Era = era;
            Agreement = agreement;
        }

        public uint Era { get; }
        public uint Agreement { get; }
        
        public IEnumerable<byte> ToByteArray()
        {
            return BitConverter.GetBytes(Era).Concat(BitConverter.GetBytes(Agreement));
        }
    }
}