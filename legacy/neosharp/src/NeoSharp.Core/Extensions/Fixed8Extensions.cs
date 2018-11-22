using System;
using System.Collections.Generic;
using System.Linq;

namespace NeoSharp.Core.Extensions
{
    public static class Fixed8Extensions
    {
        /// <summary>
        /// Fixed8 Sum
        /// </summary>
        /// <param name="source">Source</param>
        /// <returns>Sum Result</returns>
        public static Money Sum(this IEnumerable<Money> source)
        {
            var sum = source.Sum(item => item.Value);
            return new Money(sum);
        }

        /// <summary>
        /// Fixed8 Sum
        /// </summary>
        /// <param name="source">Source</param>
        /// <param name="selector">Selector</param>
        /// <returns>Sum Result</returns>
        public static Money Sum<TSource>(this IEnumerable<TSource> source, Func<TSource, Money> selector)
        {
            return source.Select(selector).Sum();
        }

        /// <summary>
        /// Returns the higher value 
        /// </summary>
        /// <param name="source">Source</param>
        /// <returns>Max value</returns>
        public static Money Max(this IEnumerable<Money> source)
        {
            var first = true;
            var currentFixed8 = Money.Zero;

            foreach (var other in source)
            {
                if (!first && currentFixed8.CompareTo(other) >= 0)
                    continue;
                currentFixed8 = other;
                first = false;
            }
            return currentFixed8;
        }

        /// <summary>
        /// Returns the lower value 
        /// </summary>
        /// <param name="source">Source</param>
        /// <returns>Min value</returns>
        public static Money Min(this IEnumerable<Money> source)
        {
            var first = true;
            var currentFixed8 = Money.Zero;

            foreach (var other in source)
            {
                if (!first && currentFixed8.CompareTo(other) <= 0)
                    continue;
                currentFixed8 = other;
                first = false;
            }

            return currentFixed8;
        }
    }
}