using System;
using System.Collections.Generic;

namespace Lachain.Utility.Utils
{
    public static class RandomUtils
    {
        public static void Shuffle<T>(this Random rng, T[] array)
        {
            var n = array.Length;
            while (n > 1)
            {
                var k = rng.Next(n--);
                var temp = array[n];
                array[n] = array[k];
                array[k] = temp;
            }
        }

        
        public static T SelectRandom<T>(this Random rng, IEnumerable<T> collection)
        {
            using var enumerator = collection.GetEnumerator();
            if (!enumerator.MoveNext()) throw new ArgumentException("Empty collection", nameof(collection));
            var current = enumerator.Current;
            var cnt = 1;
            while (enumerator.MoveNext())
            {
                ++cnt;
                if (rng.Next(cnt) == 0)
                {
                    current = enumerator.Current;
                }
            }
            return current;
        }
    }
}