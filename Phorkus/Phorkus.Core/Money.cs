using System;
using System.Numerics;
using Phorkus.Core.Proto;

namespace Phorkus.Core
{
    public class Money : IComparable<Money>, IEquatable<Money>, IFormattable
    {
        private static readonly BigInteger D = BigInteger.Parse("1000000000000000000"); // 10^18
        private static readonly BigInteger MaxIntegralPart = D;

        public static readonly Money MaxValue = new Money(D * MaxIntegralPart);
        public static readonly Money MinValue = new Money(-D * MaxIntegralPart);
        public static readonly Money One = new Money(D);
        public static readonly Money Wei = new Money(1);
        public static readonly Money Zero = new Money(0);

        public readonly BigInteger Value;

        public Money(BigInteger value)
        {
            Value = value;
        }

        public Money(UInt256 value)
        {
            throw new NotImplementedException();
        }

        public UInt256 ToUInt256()
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        public static bool TryParse(string s, out Money result)
        {
            throw new NotImplementedException();
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
            // (decimal) D is exact, multiplication is not
            var wei = new BigInteger(value * (decimal) D);
            return new Money(wei);
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
            throw new NotImplementedException();
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