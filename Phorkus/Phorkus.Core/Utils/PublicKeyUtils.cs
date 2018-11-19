using System;
using Google.Protobuf;
using Phorkus.Core.Proto;

namespace Phorkus.Core.Utils
{
    public static class PublicKeyUtils
    {
        public static PublicKey ToPublicKey(this byte[] buffer)
        {
            if (buffer.Length != 64)
                throw new ArgumentOutOfRangeException(nameof(buffer));
            return new PublicKey
            {
                Buffer = ByteString.CopyFrom(buffer)
            };
        }
    }
}