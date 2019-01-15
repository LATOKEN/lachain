using System.Collections.Generic;
using Phorkus.Proto;

namespace Phorkus.Utility
{
    public class UInt256Comparer : IComparer<UInt256>
    {
        public int Compare(UInt256 x, UInt256 y)
        {
            if (x == null)
                return y == null ? 0 : -1;
            if (y == null)
                return 1;
            for (var i = 0; i < x.Buffer.Length; ++i)
            {
                if (x.Buffer[i] != y.Buffer[i])
                    return x.Buffer[i] - y.Buffer[i];
            }
            return 0;
        }
    }
}