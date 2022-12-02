using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Utility.Serialization;
using Nethereum.RLP;

namespace Lachain.Crypto
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
            return HashCode.Combine(Id, Data.Length);
        }

        public byte[] ToByteArray()
        {
            var list = new List<byte[]>
            {
                Id.ToBytes().ToArray(),
                Data
            };
            return RLP.EncodeList(list.Select(RLP.EncodeElement).ToArray());
        }
        public static RawShare FromByteArray(byte[] bytes)
        {
            var decoded = (RLPCollection) RLP.Decode(bytes.ToArray());
            var id = decoded[0].RLPData.AsReadOnlySpan().ToInt32();
            var bb = decoded[1].RLPData;
            return new RawShare(bb, id);
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