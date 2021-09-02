using System;
using System.Linq;
using System.Numerics;
using Google.Protobuf;
using Lachain.Proto;

namespace Lachain.Utility.Utils
{
    public static class UInt64Utils
    {
        public static byte[] ToBytes(ulong x)
        {
            byte[] bytes = new byte[8];
            for(int i=0; i<8; i++)
            {
                bytes[i] = (byte) ((x >> (8 * i)) & 0xFF);
            }
            return bytes;
        }

        public static ulong FromBytes(byte[] bytes)
        {
            ulong x = 0;
            for(int i=0; i<8; i++)
            {
                x |= (((ulong)bytes[i])<< (8 * i)) ;
            }
            return x;
        }
    }
}