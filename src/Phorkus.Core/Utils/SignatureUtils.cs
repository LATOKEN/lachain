using System;
using Google.Protobuf;
using Phorkus.Proto;

namespace Phorkus.Core.Utils
{
    public static class SignatureUtils
    {
        public static Signature Zero = new Signature
        {
            Buffer = ByteString.CopyFrom(new byte[64])
        };

        public static bool IsZero(this Signature signature)
        {
            return Zero.Equals(signature);
        }
        
        public static Signature ToSignature(this byte[] signature)
        {
            if (signature.Length != 64)
                throw new ArgumentOutOfRangeException(nameof(signature));
            return new Signature
            {
                Buffer = ByteString.CopyFrom(signature)
            };
        }
    }
}