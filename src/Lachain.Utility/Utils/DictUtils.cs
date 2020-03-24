using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace Lachain.Utility.Utils
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

        public static V ComputeIfAbsent<U, V>(this IDictionary<U, V> dictionary, U key, Func<U, V> fn)
        {
            if (!dictionary.TryGetValue(key, out var oldValue))
            {
                return dictionary[key] = fn(key);
            }

            return oldValue;
        }

        public static V Compute<U, V>(this IDictionary<U, V> dictionary, U key, Func<U, V, V> fn)
        {
            if (!dictionary.TryGetValue(key, out var oldValue))
            {
                return dictionary[key] = fn(key, default);
            }

            return dictionary[key] = fn(key, oldValue);
        }
    }
}