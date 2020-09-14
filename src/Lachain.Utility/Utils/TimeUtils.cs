using System;

namespace Lachain.Utility.Utils
{
    public static class TimeUtils
    {
        private static readonly DateTime WhenTheUniverseWasBorn = new DateTime(1970, 1, 1);

        public static ulong CurrentTimeMillis()
        {
            return (ulong) DateTime.UtcNow.Subtract(WhenTheUniverseWasBorn).TotalMilliseconds;
        }

        public static TimeSpan Multiply(TimeSpan timeSpan, double factor)
        {
            if (double.IsNaN(factor))
                throw new ArgumentException("Argument cannot be null: ", nameof(factor));
            double num = Math.Round(timeSpan.Ticks * factor);
            if (num > long.MaxValue || num < long.MinValue)
                throw new OverflowException("Timespan overflow in multiply operation");
            return TimeSpan.FromTicks((long) num);
        }

        public static DateTimeOffset ToDateTime(this long timestampMs)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(timestampMs);
        }

        public static DateTimeOffset ToDateTime(this ulong timestampMs)
        {
            return ((long) timestampMs).ToDateTime();
        }
    }
}