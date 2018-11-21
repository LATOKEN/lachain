using System;
using System.Globalization;
using System.Numerics;
using Phorkus.Core.Proto;
using Phorkus.Core.Utils;

namespace Phorkus.Core.Blockchain
{
    public class Money : IComparable<Money>, IEquatable<Money>, IFormattable
    {
        private const int DecimalDigits = 18;
        
        private static readonly BigInteger D = BigInteger.Pow(10, DecimalDigits);
        private static readonly BigInteger MaxIntegralPart = D - 1;
        private static readonly BigInteger MaxRawValue = MaxIntegralPart * D + D - 1;
        private static readonly BigInteger MinRawValue = -MaxRawValue;

        public static readonly Money MaxValue = new Money(D * MaxIntegralPart);
        public static readonly Money MinValue = new Money(-D * MaxIntegralPart);
        public static readonly Money One = new Money(D);
        public static readonly Money Wei = new Money(1);
        public static readonly Money Zero = new Money(0);

        private readonly BigInteger Value;

        public Money(BigInteger value)
        {
            if (value > MaxRawValue || value < MinRawValue) throw new ArgumentOutOfRangeException(nameof(value));
            Value = value;
        }

        public Money(UInt256 value)
        {
            Value = value.ToBigInteger();
        }

        public BigInteger ToWei()
        {
            return Value;
        }
        
        public UInt256 ToUInt256()
        {
            return Value.ToUInt256();
        }

        public int CompareTo(Money other)
        {
            return Value.CompareTo(other.Value);
        }
        
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((Money) obj);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public bool Equals(Money other)
        {
            return other != null && Value == other.Value;
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            throw new NotImplementedException();
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

        public static bool TryParse(string s, out Money result)
        {
            var parts = s.Split(new[] {CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator}, StringSplitOptions.None);
            if (parts.Length == 0 || parts.Length > 2 || !BigInteger.TryParse(parts[0], out var integralPart))
            {
                result = default(Money);
                return false;
            }

            if (parts.Length == 1)
            {
                result = new Money(integralPart * D);
                return true;
            }

            if (!BigInteger.TryParse(parts[1], out var decimalPart))
            {
                result = default(Money);
                return false;
            }

            if (decimalPart < 0 || decimalPart > D)
            {
                result = default(Money);
                return false;
            }

            var value = integralPart * D + decimalPart * BigInteger.Pow(10, DecimalDigits - parts[1].Length);
            if (value < MinRawValue || value > MaxRawValue)
            {
                result = default(Money);
                return false;
            }
            result = new Money(value);
            return true;
        }

        public Money Abs()
        {
            return Value >= 0 ? this : new Money(-Value);
        }

        public Money Ceiling()
        {
            var remainder = Value % D;

            if (remainder == 0) return this;
            return remainder > 0 ? new Money(Value - remainder + D) : new Money(Value - remainder);
        }

        public static Money FromDecimal(decimal value)
        {
            // (decimal) D is exact, multiplication is not
            return new Money(new BigInteger(value * (decimal) D));
        }

        public static explicit operator decimal(Money value)
        {
            return (decimal) value.Value / (decimal) D;
        }

        public static explicit operator long(Money value)
        {
            return (long) BigInteger.Divide(value.Value, D);
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
            return new Money(x.Value * y);
        }

        public static Money operator /(Money x, long y)
        {
            return new Money(x.Value / y);
        }

        public static Money operator +(Money x, Money y)
        {
            return new Money(x.Value + y.Value);
        }

        public static Money operator -(Money x, Money y)
        {
            return new Money(x.Value - y.Value);
        }

        public static Money operator -(Money value)
        {
            return new Money(-value.Value);
        }
    }
}