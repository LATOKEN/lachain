using System;
using System.Linq;
using System.Numerics;
using Google.Protobuf;
using Phorkus.Core.Cryptography;
using Phorkus.Core.Proto;

namespace Phorkus.Core.Utils
{
    public static class UInt160Utils
    {
        public static readonly UInt160 Zero = new byte[20].ToUInt160();
       
        public static bool IsZero(this UInt160 value)
        {
            return Zero.Equals(value);
        }
        
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

        public static BigInteger ToBigInteger(this UInt160 value)
        {
            return new BigInteger(value.Buffer.ToByteArray().Reverse().Concat(new byte[] {0}).ToArray());
        }
        
        public static UInt160 ToUInt160(this BigInteger value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            var bytes = value.ToByteArray();
            if (bytes.Length > 21 || bytes.Length == 20 && bytes[20] != 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            return bytes.Take(20).Concat(new byte[20 - bytes.Length]).ToArray().ToUInt160();
        }

        public static bool IsValid(this UInt160 value)
        {
            return value.Buffer != null && value.Buffer.Length == 20;
        }
    }
}