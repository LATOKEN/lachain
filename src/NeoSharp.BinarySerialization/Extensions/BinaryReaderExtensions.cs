using System;
using System.IO;
using System.Text;

namespace NeoSharp.BinarySerialization.Extensions
{
    public static class BinaryReaderExtensions
    {
        public static byte[] ReadVarBytes(this BinaryReader reader, uint max = 0X7fffffc7)
        {
            return reader.ReadBytes((int)reader.ReadVarInt(max));
        }

        public static ulong ReadVarInt(this BinaryReader reader, ulong max = ulong.MaxValue)
        {
            if (max <= 0)
                throw new ArgumentOutOfRangeException(nameof(max));
            
            var fb = reader.ReadByte();
            ulong value;
            if (fb == 0xFD)
                value = reader.ReadUInt16();
            else if (fb == 0xFE)
                value = reader.ReadUInt32();
            else if (fb == 0xFF)
                value = reader.ReadUInt64();
            else
                value = fb;

            if (value > max)
                throw new FormatException("MaxLength");

            return value;
        }

        public static string ReadVarString(this BinaryReader reader, uint max = 0X7fffffc7)
        {
            return Encoding.UTF8.GetString(reader.ReadVarBytes(max));
        }
    }
}