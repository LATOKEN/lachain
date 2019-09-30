using System;
using System.Collections.Concurrent;

namespace Phorkus.Consensus
{
    public class RandomSamplingQueue<T>
    {
        private readonly ConcurrentQueue<T> _queue = new ConcurrentQueue<T>();
        private readonly Random _rnd = new Random();

        public bool IsEmpty => _queue.IsEmpty;
        public int Count => _queue.Count;
        public double RepeatProbability { get; set; } = 0;

        public bool TryDequeue(out T result)
        {
            var success = _queue.TryDequeue(out result);
            if (success && _rnd.NextDouble() < RepeatProbability)
            {
                _queue.Enqueue(result);
            }
            return success;
        }

        private bool TryTake(int k, out T result)
        {
            if (k >= _queue.Count)
            {
                result = default(T);
                return false;
            }

            for (var i = 0; i < k; ++i)
            {
                TryDequeue(out var res);
                Enqueue(res);
            }

            return TryDequeue(out result);
        }

        public bool TrySample(out T result)
        {
            var size = _queue.Count;
            var k = _rnd.Next(0, size - 1);
            return TryTake(k, out result);
        }
        
        public bool TryTakeLast(out T result)
        {
            var size = _queue.Count;
            return TryTake(size - 1, out result);
        }

        public void Enqueue(T item)
        {
            _queue.Enqueue(item);
        }

    }
}