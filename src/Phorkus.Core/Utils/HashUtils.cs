using Google.Protobuf;
using Phorkus.Crypto;
using Phorkus.Proto;

namespace Phorkus.Core.Utils
{
    public static class HashUtils
    {
        public static UInt256 ToHash256<T>(this T t)
            where T : IMessage<T>
        {
            return new UInt256
            {
                Buffer = ByteString.CopyFrom(t.ToByteArray().Sha256())
            };
        }

        public static UInt160 ToHash160<T>(this T t)
            where T : IMessage<T>
        {
            return new UInt160
            {
                Buffer = ByteString.CopyFrom(t.ToByteArray().Ripemd160())
            };
        }
        
        public static UInt160 ToHash160(this byte[] buffer)
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