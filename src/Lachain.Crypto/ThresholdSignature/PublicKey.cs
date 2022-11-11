using System;
using Lachain.Crypto.ThresholdEncryption;
using Lachain.Utility.Serialization;
using MCL.BLS12_381.Net;

namespace Lachain.Crypto.ThresholdSignature
{
    public class PublicKey : IEquatable<PublicKey>, IFixedWidth
    {
        public PublicKey(G1 rawKey)
        {
            RawKey = rawKey;
        }

        public G1 RawKey { get; }

        public bool ValidateSignature(Signature signature, byte[] message)
        {
            var mappedMessage = new G2();
            mappedMessage.SetHashOf(message);
            return GT.Pairing(RawKey, mappedMessage).Equals(GT.Pairing(G1.Generator, signature.RawSignature));
        }

        public EncryptedShare Encrypt(IRawShare rawShare)
        {
            var r = Fr.GetRandom();
            var u = G1.Generator * r;
            var shareBytes = rawShare.ToBytes();
            var t = RawKey * r;
            var v = Utils.XorWithHash(t, shareBytes);
            var w = Utils.HashToG2(u, v) * r;
            return new EncryptedShare(u, v, w, rawShare.Id);
        }

        public bool VerifyShare(EncryptedShare share, PartiallyDecryptedShare ps)
        {
            var h = Utils.HashToG2(share.U, share.V);
            return GT.Pairing(ps.Ui, h).Equals(GT.Pairing(RawKey, share.W));
        }

        public bool VerifyFullDecryptedShare(EncryptedShare share, G1 interpolation)
        {
            var h = Utils.HashToG2(share.U, share.V);
            return GT.Pairing(interpolation, h).Equals(GT.Pairing(RawKey, share.W));
        }

        public bool Equals(PublicKey other)
        {
            return other != null && RawKey.Equals(other.RawKey);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((PublicKey) obj);
        }

        public override int GetHashCode()
        {
            return RawKey.GetHashCode();
        }

        public static int Width()
        {
            return G1.ByteSize;
        }

        public void Serialize(Memory<byte> bytes)
        {
            RawKey.ToBytes().CopyTo(bytes);
        }

        public static PublicKey FromBytes(ReadOnlyMemory<byte> bytes)
        {
            return new PublicKey(G1.FromBytes(bytes.ToArray()));
        }
    }
}