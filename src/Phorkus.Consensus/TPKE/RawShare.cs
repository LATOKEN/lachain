using System;
using System.Linq;

namespace Phorkus.Consensus.TPKE
{
    public class RawShare : IRawShare
    {
        protected bool Equals(RawShare other)
        {
            return Id == other.Id && Data.SequenceEqual(other.Data);
        }

        public bool Equals(IRawShare other)
        {
            return Equals((object) other);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((RawShare) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Id * 397) ^ (Data != null ? Data.GetHashCode() : 0);
            }
        }

        public int Id { get; }
        public byte[] Data { get; }

        public RawShare(byte[] data, int id)
        {
            Data = data;
            Id = id;
        }
        
        public byte[] ToBytes()
        {
            return Data;
        }
    }
}