using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Phorkus.Crypto.MCL.BLS12_381;
using Phorkus.Proto;

namespace Phorkus.Consensus.TPKE
{
    public class TPKEVerificationKey : IEquatable<TPKEVerificationKey>
    {
        public G1 Y;
        public int t;
        public G2[] Zs;

        public TPKEVerificationKey(G1 _Y, int _t, G2[] zs)
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

        public static TPKEVerificationKey FromProto(TPKEVerificationKeyMessage enc)
        {
            var Y = G1.FromBytes(enc.Y.ToByteArray());
            var t = enc.T;
            var Zs = new List<G2>();
            foreach (var z in enc.Zs)
            {
                Zs.Add(G2.FromBytes(z.ToByteArray()));
            }
            return new TPKEVerificationKey(Y, t, Zs.ToArray());
        }

        public bool Verify(EncryptedShare share, PartiallyDecryptedShare part)
        {
            // todo check part.Id
            if (!Mcl.Pairing(G1.Generator, share.W).Equals(Mcl.Pairing(share.U, TPKEUtils.H(share.U, share.V))))
            {
                return false;
            }

            if (!Mcl.Pairing(part.Ui, G2.Generator).Equals(Mcl.Pairing(share.U, Zs[part.DecryptorId])))
            {
                return false;
            }

            return true;
        }

        public bool Equals(TPKEVerificationKey other)
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
            return Equals((TPKEVerificationKey) obj);
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
    }
}