using System;
using System.ComponentModel;
using System.Globalization;
using NeoSharp.BinarySerialization;
using NeoSharp.Core.Converters;

namespace NeoSharp.Core
{
    /// <summary>
    /// Accurate to 10^-8 64-bit fixed-point numbers minimize rounding errors.
    /// By controlling the accuracy of the multiplier, rounding errors can be completely eliminated.
    /// </summary>
    [BinaryTypeSerializer(typeof(MoneyTypeConverter))]
    [TypeConverter(typeof(MoneyTypeConverter))]
    public class Money : IComparable<Money>, IEquatable<Money>, IFormattable
    {
        private const long D = 100_000_000;
        private const ulong QUO = (1ul << 63) / (D >> 1);
        private const ulong REM = ((1ul << 63) % (D >> 1)) << 1;

        public const int Size = sizeof(long);

        public static readonly Money MaxValue = new Money(long.MaxValue);
        public static readonly Money MinValue = new Money(long.MinValue);
        public static readonly Money One = new Money(D);
        public static readonly Money Satoshi = new Money(1);
        public static readonly Money Zero = default(Money);

        public readonly long Value;

        public Money(byte[] bytes)
        {
            if (bytes.Length != 32)
                throw new ArgumentOutOfRangeException(nameof(bytes));
            throw new NotImplementedException();            
        }
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="value">Value</param>
        public Money(long value)
        {
            Value = value;
        }
        
        public int CompareTo(Money other)
        {
            return Value.CompareTo(other.Value);
        }

        public bool Equals(Money other)
        {
            return Value.Equals(other.Value);
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            return ((decimal)this).ToString(format, formatProvider);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Money value)) return false;

            return Equals(value);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return ((decimal)this).ToString(CultureInfo.InvariantCulture);
        }

        public string ToString(string format)
        {
            return ((decimal)this).ToString(format);
        }

        public byte[] ToArray()
        {
            throw new NotImplementedException();
        }

        public static Money Parse(string s)
        {
            return FromDecimal(decimal.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture));
        }

        public static bool TryParse(string s, out Money result)
        {
            if (!decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal d))
            {
                result = default(Money);
                return false;
            }

            d *= D;

            if (d < long.MinValue || d > long.MaxValue)
            {
                result = default(Money);
                return false;
            }

            result = new Money((long)d);
            return true;
        }

        public Money Abs()
        {
            if (Value >= 0) return this;
            return new Money(-Value);
        }

        public Money Ceiling()
        {
            var remainder = Value % D;

            if (remainder == 0) return this;
            if (remainder > 0) return new Money(Value - remainder + D);

            return new Money(Value - remainder);
        }

        public static Money FromDecimal(decimal value)
        {
            value *= D;

            if (value < long.MinValue || value > long.MaxValue)
            {
                throw new OverflowException();
            }

            return new Money((long)value);
        }

        public static explicit operator decimal(Money value)
        {
            return value.Value / (decimal)D;
        }

        public static explicit operator long(Money value)
        {
            return value.Value / D;
        }

        public static bool operator ==(Money x, Money y)
        {
            return x.Equals(y);
        }

        public static bool operator !=(Money x, Money y)
        {
            return !x.Equals(y);
        }

        public static bool operator >(Money x, Money y)
        {
            return x.CompareTo(y) > 0;
        }

        public static bool operator <(Money x, Money y)
        {
            return x.CompareTo(y) < 0;
        }

        public static bool operator >=(Money x, Money y)
        {
            return x.CompareTo(y) >= 0;
        }

        public static bool operator <=(Money x, Money y)
        {
            return x.CompareTo(y) <= 0;
        }

        public static Money operator *(Money x, Money y)
        {
            var sign = Math.Sign(x.Value) * Math.Sign(y.Value);
            var ux = (ulong)Math.Abs(x.Value);
            var uy = (ulong)Math.Abs(y.Value);
            var xh = ux >> 32;
            var xl = ux & 0x00000000fffffffful;
            var yh = uy >> 32;
            var yl = uy & 0x00000000fffffffful;
            var rh = xh * yh;
            var rm = xh * yl + xl * yh;
            var rl = xl * yl;
            var rmh = rm >> 32;
            var rml = rm << 32;

            rh += rmh;
            rl += rml;

            if (rl < rml) ++rh;
            if (rh >= D) throw new OverflowException();

            var rd = rh * REM + rl;

            if (rd < rl) ++rh;

            var r = rh * QUO + rd / D;

            return new Money((long)r * sign);
        }

        public static Money operator *(Money x, long y)
        {
            return new Money(checked(x.Value * y));
        }

        public static Money operator /(Money x, long y)
        {
            return new Money(x.Value / y);
        }

        public static Money operator +(Money x, Money y)
        {
            return new Money(checked(x.Value + y.Value));
        }

        public static Money operator -(Money x, Money y)
        {
            return new Money(checked(x.Value - y.Value));
        }

        public static Money operator -(Money value)
        {
            return new Money(-value.Value);
        }
    }
}
