using System;
using System.IO;
using System.Text;

namespace Phorkus.Core.Uilts
{
    public static class BinaryUtils
    {
        public static int WriteLength(this BinaryWriter writer, long value)
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (value < 0xFD)
            {
                writer.Write((byte) value);
                return 1;
            }
            
            if (value <= 0xFFFF)
            {
                writer.Write((byte) 0xFD);
                writer.Write((ushort) value);
                return 3;
            }

            if (value <= 0xFFFFFFFF)
            {
                writer.Write((byte) 0xFE);
                writer.Write((uint) value);
                return 5;
            }

            writer.Write((byte) 0xFF);
            writer.Write(value);
            return 9;
        }

        public static ulong ReadLength(this BinaryReader reader, ulong limit = ulong.MaxValue)
        {
            if (limit <= 0)
                throw new ArgumentOutOfRangeException(nameof(limit));
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
            if (value > limit)
                throw new FormatException("MaxLength");
            return value;
        }
        
        public static int WriteUtf8String(this BinaryWriter writer, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            var length = writer.WriteLength(bytes.Length);
            writer.Write(bytes);
            length += bytes.Length;
            writer.Flush();
            return length;
        }
        
        public static string ReadUtf8String(this BinaryReader reader, ulong limit = ulong.MaxValue)
        {
            var length = reader.ReadLength(limit);
            var bytes = reader.ReadBytes((int) length);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}