using Google.Protobuf;
using Phorkus.Crypto;
using Phorkus.Proto;

namespace Phorkus.Core.Utils
{
    public static class HashUtils
    {
        public static UInt160 Tohash160(this byte[] buffer)
        {
            return new UInt160
            {
                Buffer = ByteString.CopyFrom(buffer.Ripemd160())
            };
        }        
        
        public static UInt256 ToHash256(this byte[] buffer)
        {
            return new UInt256
            {
                Buffer = ByteString.CopyFrom(buffer.Sha256())
            };
        }
    }
}