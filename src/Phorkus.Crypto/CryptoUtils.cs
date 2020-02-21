using System;
using Google.Protobuf;
using Phorkus.Proto;

namespace Phorkus.Crypto
{
    public static class CryptoUtils
    {
        public const int PublicKeyLength = 33;

        public static ECDSAPublicKey ToPublicKey(this byte[] buffer)
        {
            if (buffer.Length != PublicKeyLength)
                throw new ArgumentOutOfRangeException(nameof(buffer));
            return new ECDSAPublicKey
            {
                Buffer = ByteString.CopyFrom(buffer)
            };
        }

        public static byte[] EncodeCompressed(this ECDSAPublicKey key)
        {
            return key.Buffer.ToByteArray();
        }

        public static ECDSAPrivateKey ToPrivateKey(this byte[] buffer)
        {
            /*if (buffer.Length != 32)
                throw new ArgumentOutOfRangeException(nameof(buffer));*/
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