using System;
using System.Collections.Generic;
using System.Linq;

namespace Lachain.Utility.Utils
{
    public static class EnumerableUtils
    {
        public static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> source, int batchSize)
        {
            using var enumerator = source.GetEnumerator();
            while (enumerator.MoveNext())
                yield return YieldBatchElements(enumerator, batchSize - 1);
        }
        
        public static IEnumerable<ReadOnlyMemory<T>> Batch<T>(this ReadOnlyMemory<T> source, int batchSize)
        {
            for (var i = 0; i < source.Length; i += batchSize)
                yield return source.Slice(i, batchSize);
        }

        private static IEnumerable<T> YieldBatchElements<T>(IEnumerator<T> source, int batchSize)
        {
            yield return source.Current;
            for (var i = 0; i < batchSize && source.MoveNext(); i++)
                yield return source.Current;
        }

        public static int FirstIndexOf<T>(this IEnumerable<T> haystack, T needle) where T : IEquatable<T>
        {
            return haystack
                .Select((t, i) => (t, i))
                .First(x => x.t.Equals(needle)).i;
        }

        public static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> source)
        {
            return source.Select((item, index) => (item, index));
        }

        public static IEnumerable<T> Flatten<T>(this IEnumerable<IEnumerable<T>> source)
        {
            return source.SelectMany(x => x);
        }

        public static IEnumerable<T> PadRight<T>(this IEnumerable<T> source, T withElement, int toSize)
        {
            var cnt = 0;
            foreach (var x in source)
            {
                yield return x;
                cnt += 1;
            }

            for (; cnt < toSize; ++cnt)
            {
                yield return withElement;
            }
        }
    }
}