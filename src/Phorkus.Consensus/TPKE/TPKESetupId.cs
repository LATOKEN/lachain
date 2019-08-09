using System;
using System.Collections.Generic;
using System.Linq;
using Phorkus.Consensus.HoneyBadger;

namespace Phorkus.Consensus.TPKE

{
    public class TPKESetupId : IProtocolIdentifier
    {
        public TPKESetupId(HoneyBadgerId honeyBadgerId)
        {
            Era = honeyBadgerId.Era;
        }
        
        public bool Equals(IProtocolIdentifier other)
        {
            throw new NotImplementedException();
        }

        public ulong Era { get; }
        public IEnumerable<byte> ToByteArray()
        {
            throw new NotImplementedException();
        }
    }
}