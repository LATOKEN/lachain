using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Utility
{
    public class Money : IComparable<Money>, IEquatable<Money>
    {
        public const int DecimalDigits = 18;
        
        private static readonly BigInteger D = BigInteger.Pow(10, DecimalDigits);
        private static readonly BigInteger MaxIntegralPart = D - 1;
        private static readonly BigInteger MaxRawValue = MaxIntegralPart * D + D - 1;
        private static readonly BigInteger MinRawValue = -MaxRawValue;

        public static readonly Money MaxValue = new Money(MaxRawValue);
        public static readonly Money MinValue = new Money(MinRawValue);
        public static readonly Money One = new Money(D);
        public static readonly Money Zero = new Money(0);
        public static readonly Money Wei = new Money(1);

        private readonly BigInteger _value;

        public Money(BigInteger value)
        {
            if (value < MinRawValue || value > MaxRawValue)
                throw new ArgumentOutOfRangeException(nameof(value));
            _value = value;
        }

        public Money(UInt256 value)
        {
            _value = value.ToBigInteger();
        }

        public BigInteger ToWei()
        {
            return _value;
        }
        
        public UInt256 ToUInt256()
        {
            return _value.ToUInt256();
        }

        public override string ToString()
        {
            var str = _value.ToString();
            if (_value == BigInteger.Zero)
                return str;
            if (str.Length - DecimalDigits > 0)
                return str.Substring(0, str.Length - DecimalDigits) + "." + str.Substring(str.Length - DecimalDigits);
            return "0." + string.Join("", Enumerable.Repeat("0", DecimalDigits - str.Length)) + str;
        }

        public int CompareTo(Money other)
        {
            return _value.CompareTo(other._value);
        }
        
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((Money) obj);
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }

        public bool Equals(Money other)
        {
            return !(other is null) && _value == other._value;
        }

        public static Money Parse(string s)
        {
            var parts = s.Split(new[] {CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator}, StringSplitOptions.None);
            if (parts.Length == 0 || parts.Length > 2) throw new FormatException("Bad formatted decimal string: " + s);
            var result = BigInteger.Parse(parts[0]) * D;
            if (parts.Length == 1) return new Money(result);
            
            var decimalPart = BigInteger.Parse(parts[1]);
            if (decimalPart < 0 || decimalPart > D) throw new FormatException("Decimal part out of range: " + s);
            result += decimalPart * BigInteger.Pow(10, DecimalDigits - parts[1].Length);
            return new Money(result);
        }

        public static bool TryParse(string s, out Money? result)
        {
            var parts = s.Split(new[] {CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator}, StringSplitOptions.None);
            if (parts.Length == 0 || parts.Length > 2 || !BigInteger.TryParse(parts[0], out var integralPart))
            {
                result = default;
                return false;
            }

            if (parts.Length == 1)
            {
                result = new Money(integralPart * D);
                return true;
            }

            if (!BigInteger.TryParse(parts[1], out var decimalPart))
            {
                result = default;
                return false;
            }

            if (decimalPart < 0 || decimalPart > D)
            {
                result = default;
                return false;
            }

            var value = integralPart * D + decimalPart * BigInteger.Pow(10, DecimalDigits - parts[1].Length);
            if (value < MinRawValue || value > MaxRawValue)
            {
                result = default;
                return false;
            }
            result = new Money(value);
            return true;
        }

        public Money Abs()
        {
            return _value >= 0 ? this : new Money(-_value);
        }

        public Money Ceiling()
        {
            var remainder = _value % D;

            if (remainder == 0) return this;
            return remainder > 0 ? new Money(_value - remainder + D) : new Money(_value - remainder);
        }

        public static Money FromDecimal(ulong value)
        {
            // (decimal) D is exact, multiplication is not
            return new Money(new BigInteger(value * (decimal) D));
        }
        
        public static Money FromDecimal(decimal value)
        {
            // (decimal) D is exact, multiplication is not
            return new Money(new BigInteger(value * (decimal) D));
        }

        public static explicit operator decimal(Money value)
        {
            return (decimal) value._value / (decimal) D;
        }

        public static explicit operator long(Money value)
        {
            return (long) BigInteger.Divide(value._value, D);
        }

        public static bool operator ==(Money x, Money y)
        {
            if (ReferenceEquals(x, y)) return true;
            return !ReferenceEquals(x, null) && x.Equals(y);
        }

        public static bool operator !=(Money x, Money y)
        {
            if (ReferenceEquals(x, y)) return false;
            return ReferenceEquals(x, null) || !x.Equals(y);
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

        public static Money operator *(Money x, long y)
        {
            return new Money(x._value * y);
        }

        public static Money operator /(Money x, long y)
        {
            return new Money(x._value / y);
        }

        public static Money operator +(Money x, Money y)
        {
            return new Money(x._value + y._value);
        }
        
        public static Money operator -(Money x, Money y)
        {
            return new Money(x._value - y._value);
        }

        public static Money operator -(Money value)
        {
            return new Money(-value._value);
        }
    }
}