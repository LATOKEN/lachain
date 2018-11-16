using System;
using Google.Protobuf;
using Phorkus.Core.Cryptography;
using Phorkus.Core.Proto;

namespace Phorkus.Core.Uilts
{
    public static class UInt256Utils
    {
        /// <summary>
        /// empty UInt256 number is 32 empty bytes 
        /// </summary>
        public static readonly UInt256 Zero = new UInt256
        {
            Buffer = ByteString.CopyFrom(new byte[32])
        };
        
        public static bool IsZero(this UInt256 value)
        {
            /* TODO: "not implemented" */
            return false;
        }

        public static UInt256 ToHash256(this byte[] buffer)
        {
            return new UInt256
            {
                Buffer = ByteString.CopyFrom(buffer.Sha256())
            };
        }
        
        public static UInt256 ToUInt256(this byte[] buffer)
        {
            if (buffer.Length != 32)
                throw new ArgumentOutOfRangeException(nameof(buffer));
            return new UInt256
            {
                Buffer = ByteString.CopyFrom(buffer)
            };
        }
    }
}