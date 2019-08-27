using System;
using System.Collections.Concurrent;

namespace Phorkus.Consensus
{
    public class RandomSamplingQueue<T>
    {
        private readonly ConcurrentQueue<T> _queue = new ConcurrentQueue<T>();
        private readonly Random _rnd = new Random();

        public bool IsEmpty => _queue.IsEmpty;

        public bool TryDequeue(out T result)
        {
            return _queue.TryDequeue(out result);
        }

        public bool TrySample(out T result)
        {
            var size = _queue.Count;
            var k = _rnd.Next(0, size - 1);
            for (var i = 0; i < k; ++i)
            {
                TryDequeue(out var res);
                Enqueue(res);
            }

            return TryDequeue(out result);
        }

        public void Enqueue(T item)
        {
            _queue.Enqueue(item);
        }
    }
}