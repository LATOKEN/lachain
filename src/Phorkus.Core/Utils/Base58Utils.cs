using System.Collections.Generic;
using System.Linq;
using Phorkus.Core.Cryptography;

namespace Phorkus.Core.Utils
{
    public static class Base58Utils
    {
        public static string ToBase58(this IEnumerable<byte> buffer)
        {
            return Base58.Encode(buffer.ToArray());
        }
    }
}