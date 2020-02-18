using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Phorkus.Consensus
{
    public class RandomSamplingQueue<T> where T : class
    {
        private readonly ConcurrentQueue<T> _queue = new ConcurrentQueue<T>();
        private readonly Random _rnd = new Random();

        public bool IsEmpty => _queue.IsEmpty;
        public int Count => _queue.Count;
        public double RepeatProbability { get; set; } = 0;

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryDequeue(out T result, double repeatProb=.0)
        {
            var success = _queue.TryDequeue(out result);
            if (success && _rnd.NextDouble() < repeatProb)
            {
                Enqueue(result);
            }
            return success;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private bool TryTake(int k, out T? result)
        {
            if (k >= _queue.Count)
            {
                result = default;
                return false;
            }

            for (var i = 0; i < k; ++i)
            {
                TryDequeue(out var res);
                Enqueue(res);
            }

            return TryDequeue(out result, repeatProb: RepeatProbability);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TrySample(out T? result)
        {
            var size = _queue.Count;
            var k = _rnd.Next(0, size - 1);
            return TryTake(k, out result);
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryTakeLast(out T? result)
        {
            var size = _queue.Count;
            return TryTake(size - 1, out result);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Enqueue(T item)
        {
            _queue.Enqueue(item);
        }

    }
}