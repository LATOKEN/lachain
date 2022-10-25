using System;
using Lachain.Crypto.ThresholdEncryption;
using Lachain.Utility.Serialization;
using MCL.BLS12_381.Net;

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

        public bool VerifyFullDecryptedShare(EncryptedShare share, G1 interpolation)
        {
            var h = Utils.HashToG2(share.U, share.V);
            return GT.Pairing(interpolation, h).Equals(GT.Pairing(_y, share.W));
        }

        public bool VerifyShare(EncryptedShare share, PartiallyDecryptedShare ps)
        {
            var h = Utils.HashToG2(share.U, share.V);
            return GT.Pairing(ps.Ui, h).Equals(GT.Pairing(_y, share.W));
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
            return sizeof(int) + G1.ByteSize;
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