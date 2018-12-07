using System;
using Google.Protobuf;
using Phorkus.Proto;

namespace Phorkus.Utility.Utils
{
    public static class SignatureUtils
    {
        public static Signature Zero = new byte[65].ToSignature();

        public static bool IsZero(this Signature signature)
        {
            return Zero.Equals(signature);
        }
        
        public static Signature ToSignature(this byte[] signature)
        {
            if (signature.Length != 65)
                throw new ArgumentOutOfRangeException(nameof(signature));
            return new Signature
            {
                Buffer = ByteString.CopyFrom(signature)
            };
        }
    }
}