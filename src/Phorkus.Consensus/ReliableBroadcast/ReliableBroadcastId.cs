using System;
using System.Collections.Generic;

namespace Phorkus.Consensus.ReliableBroadcast
{
    public class ReliableBroadcastId: IProtocolIdentifier
    {
        public ulong AssociatedValidatorId { get; }
        
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