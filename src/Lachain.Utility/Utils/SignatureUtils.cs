using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Lachain.Proto;

namespace Lachain.Utility.Utils
{
    public static class SignatureUtils
    {
        public const int Length = 65;

        public static Signature Zero = new byte[Length].ToSignature();

        public static bool IsZero(this Signature signature)
        {
            return Zero.
                Equals(signature);
        }

        public static Signature ToSignature(this IEnumerable<byte> signature)
        {
            var bytes = signature.ToArray();
            if (bytes.Length != Length)
                throw new ArgumentOutOfRangeException(nameof(signature));
            return new Signature {Buffer = ByteString.CopyFrom(bytes)};
        }

        public static byte[] Encode(this Signature signature)
        {
            return signature.Buffer.ToByteArray();
        }

        public static string ToHex(this Signature signature)
        {
            return signature.Encode().ToHex();
        }
    }
}