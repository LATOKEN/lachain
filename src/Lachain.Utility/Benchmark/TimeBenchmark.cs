using System;
using Lachain.Utility.Utils;

namespace Lachain.Utility.Benchmark
{
    public class TimeBenchmark
    {
        public ulong TotalTime { get; private set; }
        public int Count { get; private set; }

        public TimeBenchmark()
        {
        }

        public void Reset()
        {
            TotalTime = 0;
            Count = 0;
        }

        public T Benchmark<T>(Func<T> action)
        {
            var startTs = TimeUtils.CurrentTimeMillis();
            var res = action.Invoke();
            TotalTime += TimeUtils.CurrentTimeMillis() - startTs;
            Count += 1;
            return res;
        }
    }
}