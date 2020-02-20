using System.Collections.Generic;

namespace Phorkus.Utility.Utils
{
    public static class DictUtils
    {
        public static V PutIfAbsent<U, V>(this IDictionary<U, V> dictionary, U key, V value)
        {
            if (!dictionary.TryGetValue(key, out var oldValue))
            {
                return dictionary[key] = value;
            }

            return oldValue;
        }
    }
}