using Lachain.Utility.Utils;
using MCL.BLS12_381.Net;

namespace Lachain.Crypto
{
    public static class MclExtensions
    {
        public static string ToHex(this Fr fr, bool prefix = true)
        {
            return fr.ToBytes().ToHex(prefix);
        }
        
        public static string ToHex(this G1 g1, bool prefix = true)
        {
            return g1.ToBytes().ToHex(prefix);
        }
        
        public static string ToHex(this G2 g2, bool prefix = true)
        {
            return g2.ToBytes().ToHex(prefix);
        }
    }
}