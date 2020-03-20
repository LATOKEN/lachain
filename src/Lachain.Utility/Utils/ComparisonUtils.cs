using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

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
    }
}