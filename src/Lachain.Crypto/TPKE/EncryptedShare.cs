using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Utility.Serialization;
using MCL.BLS12_381.Net;

namespace Lachain.Crypto.TPKE
{
    public class EncryptedShare : IEquatable<EncryptedShare>
    {
        
        public G1 U { get; }
        public byte[] V { get; }
        public G2 W { get; }
        public int Id { get; }

        public EncryptedShare(G1 u, byte[] v, G2 w, int id)
        {
            U = u;
            V = v;
            W = w;
            Id = id;
        }
        
        public IEnumerable<byte> ToBytes()
        {
            return Id.ToBytes().Concat(U.ToBytes()).Concat(W.ToBytes()).Concat(V);
        }

        public static EncryptedShare FromBytes(ReadOnlyMemory<byte> bytes)
        {
            var res = FixedWithSerializer.Deserialize(bytes, out var offset, typeof(int), typeof(G1), typeof(G2));
            return new EncryptedShare((G1) res[1], bytes.Slice(offset).ToArray(), (G2) res[2], (int) res[0]);
        }

        public bool Equals(EncryptedShare? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return U.Equals(other.U) && V.SequenceEqual(other.V) && W.Equals(other.W) && Id == other.Id;
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((EncryptedShare) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(U, V, W, Id);
        }
    }
}