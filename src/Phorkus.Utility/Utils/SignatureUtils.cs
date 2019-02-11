using System;
using Google.Protobuf;
using Phorkus.Proto;

namespace Phorkus.Utility.Utils
{
    public static class SignatureUtils
    {
        public const int Length = 65;
        
        public static Signature Zero = new byte[Length].ToSignature();

        public static bool IsZero(this Signature signature)
        {
            return Zero.Equals(signature);
        }
        
        public static Signature ToSignature(this byte[] signature)
        {
            if (signature.Length != Length)
                throw new ArgumentOutOfRangeException(nameof(signature));
            return new Signature
            {
                Buffer = ByteString.CopyFrom(signature)
            };
        }
    }
}