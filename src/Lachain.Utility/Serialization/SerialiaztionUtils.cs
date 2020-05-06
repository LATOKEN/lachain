using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Utility.Utils;

namespace Lachain.Utility.Serialization
{
    public static class SerializationUtils
    {
        public static int Size(this IFixedWidth obj)
        {
            return FixedWidthSize(obj.GetType());
        }

        internal static int FixedWidthSize(Type obj)
        {
            return (int) (obj.GetMethod("Width") ?? throw new InvalidOperationException())
                .Invoke(null, Array.Empty<object>());
        }

        internal static int GetTypeWidth(Type type)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Boolean:
                case TypeCode.SByte:
                case TypeCode.Byte:
                    return 1;
                case TypeCode.UInt16:
                case TypeCode.Int16:
                    return 2;
                case TypeCode.UInt32:
                case TypeCode.Int32:
                    return 4;
                case TypeCode.UInt64:
                case TypeCode.Int64:
                    return 8;
                case TypeCode.Object:
                    return FixedWidthSize(type);
                case TypeCode.Single:
                case TypeCode.String:
                case TypeCode.Char:
                case TypeCode.DateTime:
                case TypeCode.DBNull:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Empty:
                    throw new ArgumentOutOfRangeException();
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        internal static void SerializeType(Memory<byte> bytes, dynamic value)
        {
            switch (Type.GetTypeCode(value.GetType()))
            {
                case TypeCode.Boolean:
                    bytes.Span[0] = (byte) (value ? 1 : 0);
                    return;
                case TypeCode.SByte:
                    bytes.Span[0] = (byte) value;
                    return;
                case TypeCode.Byte:
                    bytes.Span[0] = value;
                    return;
                case TypeCode.UInt16:
                    ((ushort) value).Serialize(bytes);
                    return;
                case TypeCode.Int16:
                    ((short) value).Serialize(bytes);
                    return;
                case TypeCode.UInt32:
                    ((uint) value).Serialize(bytes);
                    return;
                case TypeCode.Int32:
                    ((int) value).Serialize(bytes);
                    return;
                case TypeCode.UInt64:
                    ((ulong) value).Serialize(bytes);
                    return;
                case TypeCode.Int64:
                    ((long) value).Serialize(bytes);
                    return;
                case TypeCode.Object:
                    ((IFixedWidth) value).Serialize(bytes);
                    return;
                case TypeCode.Single:
                case TypeCode.String:
                case TypeCode.Char:
                case TypeCode.DateTime:
                case TypeCode.DBNull:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Empty:
                    throw new ArgumentOutOfRangeException();
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        internal static dynamic DeserializeType(ReadOnlyMemory<byte> bytes, Type type)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Boolean:
                    return bytes.Span[0] != 0;
                case TypeCode.SByte:
                    return (sbyte) bytes.Span[0];
                case TypeCode.Byte:
                    return bytes.Span[0];
                case TypeCode.UInt16:
                    return bytes.Span.ToUInt16();
                case TypeCode.Int16:
                    return bytes.Span.ToInt16();
                case TypeCode.UInt32:
                    return bytes.Span.ToUInt32();
                case TypeCode.Int32:
                    return bytes.Span.ToInt32();
                case TypeCode.UInt64:
                    return bytes.Span.ToUInt64();
                case TypeCode.Int64:
                    return bytes.Span.ToInt64();
                case TypeCode.Object:
                    return (
                        type.GetMethod("FromBytes") ??
                        throw new InvalidOperationException($"Cannot deserialize type {type} without FromBytes method")
                    ).Invoke(null, new object[] {bytes});
                case TypeCode.Single:
                case TypeCode.String:
                case TypeCode.Char:
                case TypeCode.DateTime:
                case TypeCode.DBNull:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Empty:
                    throw new ArgumentOutOfRangeException();
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static byte[] ToBytes(this IFixedWidth obj)
        {
            var result = new byte[obj.Size()];
            obj.Serialize(result);
            return result;
        }

        public static string ToHex(this IFixedWidth obj)
        {
            return obj.ToBytes().ToHex();
        }

        private static ReadOnlySpan<byte> ToLittleEndian(ReadOnlySpan<byte> span)
        {
            return BitConverter.IsLittleEndian ? span : span.ToArray().Reverse().ToArray();
        }

        public static IEnumerable<byte> ToBytes(this short x)
        {
            return BitConverter.IsLittleEndian ? BitConverter.GetBytes(x) : BitConverter.GetBytes(x).Reverse();
        }

        public static IEnumerable<byte> ToBytes(this ushort x)
        {
            return BitConverter.IsLittleEndian ? BitConverter.GetBytes(x) : BitConverter.GetBytes(x).Reverse();
        }

        public static IEnumerable<byte> ToBytes(this int x)
        {
            return BitConverter.IsLittleEndian ? BitConverter.GetBytes(x) : BitConverter.GetBytes(x).Reverse();
        }

        public static IEnumerable<byte> ToBytes(this uint x)
        {
            return BitConverter.IsLittleEndian ? BitConverter.GetBytes(x) : BitConverter.GetBytes(x).Reverse();
        }

        public static IEnumerable<byte> ToBytes(this long x)
        {
            return BitConverter.IsLittleEndian ? BitConverter.GetBytes(x) : BitConverter.GetBytes(x).Reverse();
        }

        public static IEnumerable<byte> ToBytes(this ulong x)
        {
            return BitConverter.IsLittleEndian ? BitConverter.GetBytes(x) : BitConverter.GetBytes(x).Reverse();
        }

        public static void Serialize(this long x, Memory<byte> bytes)
        {
            BitConverter.TryWriteBytes(bytes.Span, x);
            if (BitConverter.IsLittleEndian) bytes.Span.Reverse();
        }

        public static void Serialize(this ulong x, Memory<byte> bytes)
        {
            BitConverter.TryWriteBytes(bytes.Span, x);
            if (BitConverter.IsLittleEndian) bytes.Span.Reverse();
        }

        public static void Serialize(this int x, Memory<byte> bytes)
        {
            BitConverter.TryWriteBytes(bytes.Span, x);
            if (BitConverter.IsLittleEndian) bytes.Span.Reverse();
        }

        public static void Serialize(this uint x, Memory<byte> bytes)
        {
            BitConverter.TryWriteBytes(bytes.Span, x);
            if (BitConverter.IsLittleEndian) bytes.Span.Reverse();
        }

        public static void Serialize(this short x, Memory<byte> bytes)
        {
            BitConverter.TryWriteBytes(bytes.Span, x);
            if (BitConverter.IsLittleEndian) bytes.Span.Reverse();
        }

        public static void Serialize(this ushort x, Memory<byte> bytes)
        {
            BitConverter.TryWriteBytes(bytes.Span, x);
            if (BitConverter.IsLittleEndian) bytes.Span.Reverse();
        }

        public static long ToInt64(this ReadOnlySpan<byte> bytes)
        {
            return BitConverter.ToInt64(ToLittleEndian(bytes));
        }

        public static ulong ToUInt64(this ReadOnlySpan<byte> bytes)
        {
            return BitConverter.ToUInt64(ToLittleEndian(bytes));
        }

        public static int ToInt32(this ReadOnlySpan<byte> bytes)
        {
            return BitConverter.ToInt32(ToLittleEndian(bytes));
        }

        public static uint ToUInt32(this ReadOnlySpan<byte> bytes)
        {
            return BitConverter.ToUInt32(ToLittleEndian(bytes));
        }

        public static short ToInt16(this ReadOnlySpan<byte> bytes)
        {
            return BitConverter.ToInt16(ToLittleEndian(bytes));
        }

        public static ushort ToUInt16(this ReadOnlySpan<byte> bytes)
        {
            return BitConverter.ToUInt16(ToLittleEndian(bytes));
        }

        public static ReadOnlySpan<byte> ToReadOnly(this Span<byte> span)
        {
            return span;
        }

        public static ReadOnlySpan<byte> AsReadOnlySpan(this byte[] array)
        {
            return array.AsSpan().ToReadOnly();
        }
    }
}