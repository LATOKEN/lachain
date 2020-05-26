using System;
using System.Collections.Generic;
using Google.Protobuf;
using Lachain.Crypto.MCL.BLS12_381;
using Lachain.Proto;
using Lachain.Utility.Serialization;

namespace Lachain.Crypto.TPKE
{
    public class PublicKey : IEquatable<PublicKey>, IFixedWidth
    {
        private readonly G1 _y;
        private readonly int _t;

        public PublicKey(G1 y, int t)
        {
            _y = y;
            _t = t;
        }

        public EncryptedShare Encrypt(IRawShare rawShare)
        {
            var r = Fr.GetRandom();
            var u = G1.Generator * r;
            var shareBytes = rawShare.ToBytes();
            var t = _y * r;
            var v = Utils.XorWithHash(t, shareBytes);
            var w = Utils.HashToG2(u, v) * r;
            return new EncryptedShare(u, v, w, rawShare.Id);
        }

        public PartiallyDecryptedShare Decode(TPKEPartiallyDecryptedShareMessage message)
        {
            var u = G1.FromBytes(message.Share.ToByteArray());
            return new PartiallyDecryptedShare(u, message.DecryptorId, message.ShareId);
        }

        public TPKEPartiallyDecryptedShareMessage Encode(PartiallyDecryptedShare share)
        {
            return new TPKEPartiallyDecryptedShareMessage
            {
                Share = ByteString.CopyFrom(share.Ui.ToBytes()),
                DecryptorId = share.DecryptorId,
                ShareId = share.ShareId
            };
        }

        public RawShare FullDecrypt(EncryptedShare share, List<PartiallyDecryptedShare> us)
        {
            if (us.Count < _t)
            {
                throw new Exception("Insufficient number of shares!");
            }

            var ids = new HashSet<int>();
            foreach (var part in us)
            {
                if (ids.Contains(part.DecryptorId))
                    throw new Exception($"Id {part.DecryptorId} was provided more than once!");
                if (part.ShareId != share.Id)
                    throw new Exception($"Share id mismatch for decryptor {part.DecryptorId}");
                ids.Add(part.DecryptorId);
            }

            var ys = new List<G1>();
            var xs = new List<Fr>();

            foreach (var part in us)
            {
                xs.Add(Fr.FromInt(part.DecryptorId + 1));
                ys.Add(part.Ui);
            }

            var u = Mcl.LagrangeInterpolateG1(xs.ToArray(), ys.ToArray());
            return new RawShare(Utils.XorWithHash(u, share.V), share.Id);
        }
        
        public static PublicKey FromBytes(ReadOnlyMemory<byte> buffer)
        {
            var res = FixedWithSerializer.Deserialize(buffer, out _, typeof(int), typeof(G1));
            return new PublicKey((G1) res[1], (int) res[0]);
        }

        public void Serialize(Memory<byte> bytes)
        {
            FixedWithSerializer.SerializeToMemory(bytes, new dynamic[] {_t, _y});
        }

        public static int Width()
        {
            return sizeof(int) + G1.Width();
        }

        public bool Equals(PublicKey? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return _y.Equals(other._y) && _t == other._t;
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((PublicKey) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_y, _t);
        }
    }
}