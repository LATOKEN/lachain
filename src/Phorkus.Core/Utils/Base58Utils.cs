using System.Collections.Generic;
using System.Linq;
using Phorkus.Crypto;

namespace Phorkus.Core.Utils
{
    public static class Base58Utils
    {
        public static byte[] Base58ToBytes(this string base58)
        {
            return Base58.Decode(base58);
        }
        
        public static string ToBase58(this IEnumerable<byte> buffer)
        {
            return Base58.Encode(buffer.ToArray());
        }
    }
}