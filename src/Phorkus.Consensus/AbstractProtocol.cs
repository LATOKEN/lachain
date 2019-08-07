using System.Collections.Concurrent;
using System.Threading;
using Phorkus.Consensus.Messages;

namespace Phorkus.Consensus
{
    public abstract class AbstractProtocol : IConsensusProtocol
    {
        private readonly ConcurrentQueue<MessageEnvelope> _queue = new ConcurrentQueue<MessageEnvelope>();
        private readonly object _queueLock = new object();
        public bool Terminated { get; protected set; }
        public abstract IProtocolIdentifier Id { get; }

        public void Start()
        {
            while (!Terminated)
            {
                lock (_queueLock)
                {
                    while (_queue.IsEmpty)
                    {
                        Monitor.Wait(_queueLock);
                    }
                }

                _queue.TryDequeue(out var msg);
                ProcessMessage(msg);
            }
        }

        public void ReceiveMessage(MessageEnvelope message)
        {
            lock (_queueLock)
            {
                _queue.Enqueue(message);
                Monitor.Pulse(_queueLock);
            }
        }

        public abstract void ProcessMessage(MessageEnvelope message);
    }
}