using System;
using System.Collections.Generic;
using Phorkus.Proto;

namespace Phorkus.Core.Utils
{
    public class UInt256Comparer : IComparer<UInt256>
    {
        public int Compare(UInt256 x, UInt256 y)
        {
            return string.Compare(x.ToHex(), y.ToHex(), StringComparison.Ordinal);
        }
    }
}