using System;
using Google.Protobuf;
using Lachain.Proto;

namespace Lachain.Crypto
{
    public static class CryptoUtils
    {
        public const int PublicKeyLength = 33;
        private static ICrypto _crypto = CryptoProvider.GetCrypto();

        public static ECDSAPublicKey ToPublicKey(this byte[] buffer)
        {
            if (buffer.Length != PublicKeyLength)
                throw new ArgumentOutOfRangeException(nameof(buffer));
            return new ECDSAPublicKey
            {
                Buffer = ByteString.CopyFrom(buffer)
            };
        }

        public static ECDSAPublicKey GetPublicKey(this ECDSAPrivateKey key)
        {
            return _crypto.ComputePublicKey(key.Buffer.ToByteArray(), true).ToPublicKey();
        }

        public static byte[] EncodeCompressed(this ECDSAPublicKey key)
        {
            return key.Buffer.ToByteArray();
        }

        public static ECDSAPrivateKey ToPrivateKey(this byte[] buffer)
        {
            if (buffer.Length != 32)
                throw new ArgumentOutOfRangeException(nameof(buffer));
            return new ECDSAPrivateKey
            {
                Buffer = ByteString.CopyFrom(buffer)
            };
        }

        public static byte[] Encode(this ECDSAPrivateKey key)
        {
            return key.Buffer.ToByteArray();
        }
    }
}