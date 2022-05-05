using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Lachain.Proto;

namespace Lachain.Utility.Utils
{
    public static class SignatureUtils
    {
        public static int Length(bool useNewChainId)
        {
            return useNewChainId ? 66 : 65;
        } 

        public static Signature ZeroNew = new byte[66].ToSignature(true);
        public static Signature ZeroOld = new byte[65].ToSignature(false);

        public static bool IsZero(this Signature signature)
        {
            return ZeroNew.Equals(signature) || ZeroOld.Equals(signature);
        }

        public static Signature ToSignature(this IEnumerable<byte> signature, bool useNewChainId)
        {
            var bytes = signature.ToArray();
            if (bytes.Length != Length(useNewChainId))
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