using Phorkus.Proto;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.Utils
{
    public static class HexUtils
    {
        public static UInt256 HexToUInt256(this string buffer)
        {
            return buffer.HexToBytes().ToUInt256();
        }

        public static UInt160 HexToUInt160(this string buffer)
        {
            return buffer.HexToBytes().ToUInt160();
        }
    }
}