using System;
using Google.Protobuf;
using Phorkus.Core.Cryptography;
using Phorkus.Core.Proto;

namespace Phorkus.Core.Uilts
{
    public static class UInt160Utils
    {
        public static readonly UInt160 Zero = new UInt160
        {
            Buffer = ByteString.CopyFrom(new byte[20])
        };
        
        public static UInt160 ToHash160(this byte[] buffer)
        {
            return new UInt160
            {
                Buffer = ByteString.CopyFrom(buffer.Ripemd160())
            };
        }
        
        public static UInt160 ToUInt160(this byte[] buffer)
        {
            if (buffer.Length != 20)
                throw new ArgumentOutOfRangeException(nameof(buffer));
            return new UInt160
            {
                Buffer = ByteString.CopyFrom(buffer)
            };
        }
    }
}