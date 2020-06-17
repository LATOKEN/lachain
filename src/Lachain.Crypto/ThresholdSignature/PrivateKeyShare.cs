using System;
using Lachain.Utility.Serialization;
using MCL.BLS12_381.Net;

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
            mappedMessage *= _privateKey;
            return new Signature(mappedMessage);
        }

        public static PrivateKeyShare FromBytes(ReadOnlyMemory<byte> buffer)
        {
            return new PrivateKeyShare(Fr.FromBytes(buffer.ToArray()));
        }

        public static int Width()
        {
            return Fr.ByteSize;
        }

        public void Serialize(Memory<byte> bytes)
        {
            _privateKey.ToBytes().CopyTo(bytes);
        }
    }
}