using System;
using System.Collections.Generic;
using System.Linq;

namespace Phorkus.Consensus.BinaryAgreement
{
    public class BinaryAgreementId : IProtocolIdentifier
    {
        public BinaryAgreementId(ulong era, ulong agreement)
        {
            Era = era;
            Agreement = agreement;
        }

        public ulong Era { get; }
        public ulong Agreement { get; }
        
        public IEnumerable<byte> ToByteArray()
        {
            return BitConverter.GetBytes(Era).Concat(BitConverter.GetBytes(Agreement));
        }
    }
}