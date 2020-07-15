using System;
using Lachain.Utility.Utils;

namespace Lachain.Utility.Benchmark
{
    public class ThroughputCalculator
    {
        public TimeSpan Interval { get; }
        public ulong LastTimeReported { get; private set; }
        public int Count { get; private set; }
        public long Sum { get; private set; }

        private readonly Action<float, int> _callback;

        public ThroughputCalculator(TimeSpan interval, Action<float, int> callback)
        {
            _callback = callback;
            Interval = interval;
        }

        public void RegisterMeasurement(long delta)
        {
            var timestamp = TimeUtils.CurrentTimeMillis();
            if (timestamp >= LastTimeReported + Interval.TotalMilliseconds)
            {
                if (LastTimeReported != 0) _callback.Invoke((float) Sum * 1000 / (timestamp - LastTimeReported), Count);
                Count = 0;
                Sum = 0;
                LastTimeReported = timestamp;
            }

            Count += 1;
            Sum += delta;
        }
    }
}