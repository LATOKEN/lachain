using System;
using System.Threading;
using Phorkus.Consensus.Messages;

namespace Phorkus.Consensus
{
    public abstract class AbstractProtocol : IConsensusProtocol
    {
//        private readonly ConcurrentQueue<MessageEnvelope> _queue = new ConcurrentQueue<MessageEnvelope>();
        private readonly RandomSamplingQueue<MessageEnvelope> _queue = new RandomSamplingQueue<MessageEnvelope>();
        private readonly object _queueLock = new object();
        public bool Terminated { get; protected set; }
        public abstract IProtocolIdentifier Id { get; }
        protected readonly IConsensusBroadcaster _broadcaster;

        private Thread _thread;

        protected IWallet _wallet;

        public int N => _wallet.N;
        public int F => _wallet.F;
    

        protected AbstractProtocol(IWallet wallet, IConsensusBroadcaster broadcaster)
        {
            _thread = new Thread(Start);
            _thread.IsBackground = true;
            _thread.Start();
            _broadcaster = broadcaster;
            _wallet = wallet;
        }

        public int GetMyId()
        {
            return _broadcaster.GetMyId();
        }

        public void WaitFinish()
        {
            _thread.Join();
        }

        public void Start()
        {
            while (!Terminated)
            {
                MessageEnvelope msg;
                lock (_queueLock)
                {
                    while (_queue.IsEmpty)
                    {
                        Monitor.Wait(_queueLock);
                    }

                    if (_broadcaster.MixMessages)
                    {
                        _queue.TrySample(out msg);
                    }
                    else
                    {
                        _queue.TryDequeue(out msg);
                    }

                }
                try
                {
                    ProcessMessage(msg);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e);
                    Terminated = true;
                    break;
                }
                
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

        public abstract void ProcessMessage(MessageEnvelope envelope);
    }
}