using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Phorkus.Crypto.MCL.BLS12_381;
using Phorkus.Proto;

namespace Phorkus.Crypto.TPKE
{
    public class VerificationKey : IEquatable<VerificationKey>
    {
        public G1 Y;
        public int t;
        public G2[] Zs;

        public VerificationKey(G1 _Y, int _t, G2[] zs)
        {
            Y = _Y;
            t = _t;
            Zs = zs;
        }

        public TPKEVerificationKeyMessage ToProto()
        {
            var tmp = new TPKEVerificationKeyMessage
            {
                Y = ByteString.CopyFrom(G1.ToBytes(Y)),
                T = t
            };

            foreach (var z in Zs)
            {
                tmp.Zs.Add(ByteString.CopyFrom(G2.ToBytes(z)));
            }

            return tmp;
        }

        public static VerificationKey FromProto(TPKEVerificationKeyMessage enc)
        {
            var Y = G1.FromBytes(enc.Y.ToByteArray());
            var t = enc.T;
            var Zs = new List<G2>();
            foreach (var z in enc.Zs)
            {
                Zs.Add(G2.FromBytes(z.ToByteArray()));
            }

            return new VerificationKey(Y, t, Zs.ToArray());
        }

        public bool Verify(EncryptedShare share, PartiallyDecryptedShare part)
        {
            // todo check part.Id
            if (!Mcl.Pairing(G1.Generator, share.W).Equals(Mcl.Pairing(share.U, Utils.H(share.U, share.V))))
            {
                return false;
            }

            if (!Mcl.Pairing(part.Ui, G2.Generator).Equals(Mcl.Pairing(share.U, Zs[part.DecryptorId])))
            {
                return false;
            }

            return true;
        }

        public bool Equals(VerificationKey other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Y.Equals(other.Y) && t == other.t && Zs.SequenceEqual(other.Zs);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((VerificationKey) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Y.GetHashCode();
                hashCode = (hashCode * 397) ^ t;
                hashCode = (hashCode * 397) ^ (Zs != null ? Zs.GetHashCode() : 0);
                return hashCode;
            }
        }


        private static byte[] EncodeG2Delimited(G2 x)
        {
            var bytes = G2.ToBytes(x);
            return BitConverter.GetBytes(bytes.Length).Concat(bytes).ToArray();
        }

        private static int DecodeG2Delimited(byte[] buffer, int index, out G2 result)
        {
            var len = BitConverter.ToInt32(buffer, index);
            result = G2.FromBytes(buffer.Skip(index + 4).Take(len).ToArray());
            return len + 4;
        }

        public byte[] ToByteArray()
        {
            var encT = BitConverter.GetBytes(t);
            var encY = G1.ToBytes(Y);
            encY = BitConverter.GetBytes(encY.Length).Concat(encY).ToArray();
            var encZs = Zs.Select(EncodeG2Delimited).Aggregate((x, y) => x.Concat(y).ToArray());
            return encT.Concat(encY).Concat(encZs).ToArray();
        }

        public static VerificationKey FromBytes(byte[] buffer)
        {
            var decT = BitConverter.ToInt32(buffer, 0);
            var idx = 4;
            var len = BitConverter.ToInt32(buffer, idx);
            idx += 4;
            var decY = G1.FromBytes(buffer.Skip(idx).Take(len).ToArray());
            var decZs = new List<G2>();
            for (idx += len; idx < buffer.Length;)
            {
                idx += DecodeG2Delimited(buffer, idx, out var decZ);
                decZs.Add(decZ);
            }
            return new VerificationKey(decY, decT, decZs.ToArray());
        }
    }
}