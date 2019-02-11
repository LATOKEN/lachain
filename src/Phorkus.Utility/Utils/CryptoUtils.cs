using System;
using Google.Protobuf;
using Phorkus.Proto;

namespace Phorkus.Utility.Utils
{
    public static class CryptoUtils
    {
        public const int PublicKeyLength = 33;
        
        public static PublicKey ToPublicKey(this byte[] buffer)
        {
            if (buffer.Length != PublicKeyLength)
                throw new ArgumentOutOfRangeException(nameof(buffer));
            return new PublicKey
            {
                Buffer = ByteString.CopyFrom(buffer)
            };
        }

        public static PrivateKey ToPrivateKey(this byte[] buffer)
        {
            /*if (buffer.Length != 32)
                throw new ArgumentOutOfRangeException(nameof(buffer));*/
            return new PrivateKey
            {
                Buffer = ByteString.CopyFrom(buffer)
            };
        }
    }
}