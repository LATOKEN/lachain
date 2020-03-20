using System;
using System.Collections.Generic;

namespace Lachain.Consensus.RootProtocol
{
    public class RootProtocolId : IProtocolIdentifier
    {

        public long Era { get; }
        
        public IEnumerable<byte> ToByteArray()
        {
            throw new NotImplementedException();
        }

        public RootProtocolId(long era)
        {
            Era = era;
        }

        public override string ToString()
        {
            return $"Root (E={Era})";
        }

        private bool Equals(RootProtocolId other)
        {
            return Era == other.Era;
        }

        public bool Equals(IProtocolIdentifier other)
        {
            return Equals((object?) other);
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((RootProtocolId) obj);
        }

        public override int GetHashCode()
        {
            return Era.GetHashCode();
        }
    }
}