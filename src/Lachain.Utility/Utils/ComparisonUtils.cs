using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Proto;

namespace Lachain.Utility.Utils
{
    public static class ComparisonUtils
    {
        public static int CompareLexicographically<T>(this IEnumerable<IComparable<T>> x, IEnumerable<T> y)
        {
            using var xit = x.GetEnumerator();
            using var yit = y.GetEnumerator();
            for (;;)
            {
                var xNext = xit.MoveNext();
                var yNext = yit.MoveNext();
                if (!xNext && !yNext) return 0;
                if (!xNext) return -1;
                if (!yNext) return 1;
                if (xit.Current is null || yit.Current is null) throw new ArgumentNullException();
                var res = xit.Current.CompareTo(yit.Current);
                if (res != 0) return res;
            }
        }

        public class ECDSAPublicKeyComparer : IComparer<ECDSAPublicKey?>
        {
            public int Compare(ECDSAPublicKey? x, ECDSAPublicKey? y)
            {
                if (x is null) return y is null ? 0 : -1;
                if (y is null) return 1;
                return x.Buffer.Cast<IComparable<byte>>().CompareLexicographically(y.Buffer);
            }
        }
    }
}