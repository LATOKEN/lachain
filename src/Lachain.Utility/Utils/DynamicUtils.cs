using System.Collections.Generic;
using System.Linq;

namespace Lachain.Utility.Utils
{
    public static class DynamicUtils
    {
        public static IEnumerable<dynamic> AsDynamic<T>(this IEnumerable<T> s)
        {
            return s.Cast<dynamic>();
        }
    }
}