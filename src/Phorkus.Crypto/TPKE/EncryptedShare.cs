using System;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Formatters.Binary;
using Phorkus.Crypto.MCL.BLS12_381;
using Phorkus.Proto;

namespace Phorkus.Crypto.TPKE
{
    public class EncryptedShare
    {
        protected bool Equals(EncryptedShare other)
        {
            return U.Equals(other.U) && V.SequenceEqual(other.V) && W.Equals(other.W) && Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((EncryptedShare) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = U.GetHashCode();
                hashCode = (hashCode * 397) ^ (V != null ? V.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ W.GetHashCode();
                hashCode = (hashCode * 397) ^ Id;
                return hashCode;
            }
        }

        public G1 U { get; }
        public byte[] V { get; }
        public G2 W { get; }
        public int Id { get; }

        public EncryptedShare(G1 _U, byte[] _V, G2 _W, int id)
        {
            U = _U;
            V = _V;
            W = _W;
            Id = id;
        }
        
        public byte[] ToByte()
        {   
            return G1.ToBytesDelimited(U).
                Concat(G2.ToBytesDelimited(W).
                    Concat(BitConverter.GetBytes(Id).
                        Concat(BitConverter.GetBytes(V.Length).
                            Concat(V)))).ToArray();
        }

        public static EncryptedShare FromByte(byte[] buf)
        {
            var lenBuf = buf.Length;
            var lenInt = 4;
            if (lenBuf == 0)
                throw new Exception("Failed to deserialize EncryptedShare");
            var szG1 = BitConverter.ToInt32(buf.Take(lenInt).ToArray(), 0);
            
            var serU = G1.FromBytes(buf.Skip(lenInt).Take(szG1).ToArray());
            var indentTo2 = lenInt + szG1;
            var szG2 = BitConverter.ToInt32(buf.Skip(indentTo2).Take(lenInt).ToArray(), 0);
            
            var serW = G2.FromBytes(buf.Skip(indentTo2 + lenInt).Take(szG2).ToArray());
            var indentToId = indentTo2 + lenInt + szG2;
            var Id = BitConverter.ToInt32(buf.Skip(indentToId).Take(lenInt).ToArray(), 0);
            
            var indentToV = indentToId + lenInt;
            var szV = BitConverter.ToInt32(buf.Skip(indentToV).Take(lenInt).ToArray(), 0);
            
            var serV = buf.Skip(indentToV + lenInt).Take(szV).ToArray();
            return new EncryptedShare(serU, serV, serW, Id);
            
        }
    }
}