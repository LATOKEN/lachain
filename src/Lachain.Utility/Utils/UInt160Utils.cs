﻿using System;
using System.Linq;
using System.Numerics;
using Google.Protobuf;
using Lachain.Proto;

namespace Lachain.Utility.Utils
{
    public static class UInt160Utils
    {
        public static readonly UInt160 Zero = new byte[20].ToUInt160();
        public static readonly UInt160 Empty = new UInt160 {Buffer = ByteString.Empty};

        public static bool IsZero(this UInt160 value)
        {
            return Zero.Equals(value);
        }

        public static UInt160 ToUInt160(this byte[] buffer)
        {
            if (buffer.Length != 20) throw new ArgumentOutOfRangeException(nameof(buffer));
            return new UInt160 {Buffer = ByteString.CopyFrom(buffer)};
        }

        public static UInt256 ToUInt256(this UInt160 value)
        {
            var buffer = new byte[32];
            Array.Copy(value.ToBytes(), 0, buffer, 12, 20);
            return buffer.ToUInt256();
        }

        public static BigInteger ToBigInteger(this UInt160 value)
        {
            return new BigInteger(value.ToBytes().Reverse().Concat(new byte[] {0}).ToArray());
        }

        public static byte[] ToBytes(this UInt160 value)
        {
            return value.Buffer.ToByteArray();
        }

        public static UInt160 ToUInt160(this BigInteger value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            var bytes = value.ToByteArray().Reverse().ToArray();
            if (bytes.Length > 21 || bytes.Length == 21 && bytes[0] != 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (bytes.Length == 21)
                bytes = bytes.Skip(1).ToArray();
            return (new byte[20 - bytes.Length]).Concat(bytes).ToArray().ToUInt160();
        }
    }
}