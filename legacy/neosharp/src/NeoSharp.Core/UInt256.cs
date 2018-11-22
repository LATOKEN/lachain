using System;
using System.Collections;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using NeoSharp.BinarySerialization;
using NeoSharp.Core.Converters;
using NeoSharp.Core.Extensions;
using NeoSharp.Cryptography;

namespace NeoSharp.Core
{
    [BinaryTypeSerializer(typeof(UInt256Converter))]
    [TypeConverter(typeof(UInt256Converter))]
    public class UInt256 : IEquatable<UInt256>, IComparable<UInt256>
    {
        public static readonly int BufferLength = 32;

        public static readonly UInt256 Zero = new UInt256();

        private readonly byte[] _buffer;

        public UInt256()
        {
            _buffer = new byte[Size];
        }

        public UInt256(byte[] value) : this()
        {
            if (value.Length != Size)
                throw new ArgumentException();

            Array.Copy(value, _buffer, _buffer.Length);
        }

        public int Size => BufferLength;
        
        public bool Equals(UInt256 other)
        {
            if (other == null)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            return _buffer.SequenceEqual(other._buffer);
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            if (ReferenceEquals(this, obj))
                return true;

            if (obj is UInt256 other)
            {
                return _buffer.SequenceEqual(other._buffer);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return _buffer.ToInt32();
        }

        public int CompareTo(UInt256 other)
        {
            return ((IStructuralComparable)_buffer).CompareTo(other._buffer, StructuralComparisons.StructuralComparer);
        }

        public byte[] ToArray()
        {
            return _buffer.ToArray();
        }

        public override string ToString()
        {
            return _buffer.Reverse().ToHexString(true);
        }

        public string ToString(bool append0X)
        {
            return _buffer.Reverse().ToHexString(append0X);
        }

        public static UInt256 FromBase58(string value)
        {
            var buffer = Crypto.Default.Base58CheckDecode(value);
            return new UInt256(buffer);
        }
        
        public static UInt256 FromHex(string value)
        {
            return new UInt256(value.HexToBytes(BufferLength * 2).Reverse().ToArray());
        }

        public static UInt256 FromDecimal(decimal value)
        {
            var bytes = (new BigInteger(value) * BigInteger.Pow(10, 18)).ToByteArray();
            if (bytes.Length >= 32)
                throw new ArgumentException(nameof(value));
            bytes = Enumerable.Repeat<byte>(0, BufferLength - bytes.Length).Concat(bytes).ToArray();
            return new UInt256(bytes);
        }
        
        public static UInt256 FromDec(string value)
        {
            var bytes = BigInteger.Parse(value).ToByteArray();
            if (bytes.Length >= 32)
                throw new ArgumentException(nameof(value));
            bytes = Enumerable.Repeat<byte>(0, BufferLength - bytes.Length).Concat(bytes).ToArray();
            return new UInt256(bytes);
        }

        public static bool TryParse(string s, out UInt256 result)
        {
            try
            {
                result = FromHex(s);
                return true;
            }
            catch
            {
                result = Zero;
                return false;
            }
        }

        public static bool operator ==(UInt256 left, UInt256 right)
        {
            return left?.Equals(right) ?? right is null;
        }

        public static bool operator !=(UInt256 left, UInt256 right)
        {
            return !(left?.Equals(right) ?? right is null);
        }

        public static bool operator >(UInt256 left, UInt256 right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator >=(UInt256 left, UInt256 right)
        {
            return left.CompareTo(right) >= 0;
        }

        public static bool operator <(UInt256 left, UInt256 right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(UInt256 left, UInt256 right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static UInt256 operator +(UInt256 left, UInt256 right)
        {
            /* TODO: "this should be optimized" */
            return new UInt256((new BigInteger(left._buffer) + new BigInteger(right._buffer)).ToByteArray());
        }

        public static UInt256 operator -(UInt256 left, UInt256 right)
        {
            /* TODO: "this should be optimized" */
            return new UInt256((new BigInteger(left._buffer) - new BigInteger(right._buffer)).ToByteArray());
        }
    }
}
