using System;
using Lachain.Crypto.MCL.BLS12_381;
using Lachain.Utility.Serialization;

namespace Lachain.Crypto.ThresholdSignature
{
    public class PrivateKeyShare : IFixedWidth
    {
        private readonly Fr _privateKey;

        public PrivateKeyShare(Fr privateKey)
        {
            _privateKey = privateKey;
        }

        public PublicKey GetPublicKeyShare()
        {
            return new PublicKey(G1.Generator * _privateKey);
        }

        public Signature HashAndSign(byte[] message)
        {
            var mappedMessage = new G2();
            mappedMessage.SetHashOf(message);
            mappedMessage.Mul(mappedMessage, _privateKey);
            return new Signature(mappedMessage);
        }

        public static PrivateKeyShare FromBytes(ReadOnlyMemory<byte> buffer)
        {
            return new PrivateKeyShare(Fr.FromBytes(buffer));
        }

        public static int Width()
        {
            return Fr.Width();
        }

        public void Serialize(Memory<byte> bytes)
        {
            _privateKey.Serialize(bytes);
        }
    }
}