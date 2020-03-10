using System;
using System.Collections.Generic;
using System.Text;

namespace Phorkus.Utility.Utils
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
            var current = default(T);
            var cnt = 0;
            foreach (var element in collection)
            {
                ++cnt;
                if (rng.Next(cnt) == 0)
                {
                    current = element;
                }
            }

            return current;
        }
    }
}