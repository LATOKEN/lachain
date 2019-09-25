using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

namespace Phorkus.Utility.Utils
{
    // Simple struct that represents subset of {false, true}
    // Note, this is declared as struct to copy by value always
    public struct BoolSet : IEquatable<BoolSet>, IComparable<BoolSet>
    {
        private readonly byte _mask;

        public BoolSet(IEnumerable<bool> enumerable)
        {
            _mask = (byte) enumerable.Select(b => b ? 2 : 1).Aggregate(0, (x, y) => x | y);
        }

        public BoolSet(params bool[] values)
        {
            _mask = (byte) values.Select(b => b ? 2 : 1).Aggregate(0, (x, y) => x | y);
        }

        private BoolSet(byte b)
        {
            _mask = b;
        }

        public BoolSet Add(bool value)
        {
            return new BoolSet((byte) (_mask | (1 << (value ? 1 : 0))));
        }

        [Pure]
        public bool Contains(bool value)
        {
            return (_mask & (1 << (value ? 1 : 0))) != 0;
        }

        public bool Contains(BoolSet other)
        {
            return (_mask & other._mask) == other._mask;
        }

        [Pure]
        public int Count()
        {
            switch (_mask)
            {
                case 0: return 0;
                case 1: return 1;
                case 2: return 1;
                case 3: return 2;
                default:
                    throw new InvalidOperationException("BoolSet internal state is corrupted");
            }
        }

        [Pure]
        public IEnumerable<bool> Values()
        {
            if ((_mask & 1) != 0) yield return false;
            if ((_mask & 2) != 0) yield return true;
        }

        public bool Equals(BoolSet other)
        {
            return _mask == other._mask;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is BoolSet other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _mask.GetHashCode();
        }

        public int CompareTo(BoolSet other)
        {
            return _mask.CompareTo(other._mask);
        }

        public override string ToString()
        {
            return $"BoolSet({_mask})";
        }
    }
}