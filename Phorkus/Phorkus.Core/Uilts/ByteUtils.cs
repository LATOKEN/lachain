using Phorkus.Core.Cryptography;

namespace Phorkus.Core.Uilts
{
    public static class ByteUtils
    {
        public static Hash256 ToHash256(this byte[] buffer)
        {
            return new Hash256(buffer.Sha256());
        }
        
        public static Hash160 ToHash160(this byte[] buffer)
        {
            return new Hash160(buffer.Ripemd160());
        }
    }
}