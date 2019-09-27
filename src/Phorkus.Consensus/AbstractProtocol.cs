using System;
using System.Collections.Concurrent;
using System.Threading;
using Phorkus.Consensus.Messages;

namespace Phorkus.Consensus
{
    public abstract class AbstractProtocol : IConsensusProtocol
    {
        private readonly ConcurrentQueue<MessageEnvelope> _queue = new ConcurrentQueue<MessageEnvelope>();
        private readonly object _queueLock = new object();
        private readonly object _resultLock = new object();
        public bool ResultEmitted { get; protected set; }
        
        public bool Terminated { get; protected set; }
        public abstract IProtocolIdentifier Id { get; }
        protected readonly IConsensusBroadcaster _broadcaster;

        private Thread _thread;

        protected IWallet _wallet;

        public int N => _wallet.N;
        public int F => _wallet.F;
    

        protected AbstractProtocol(IWallet wallet, IConsensusBroadcaster broadcaster)
        {
            _thread = new Thread(Start) {IsBackground = true};
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

        public void WaitResult()
        {
            while (!ResultEmitted)
            {
                lock (_resultLock)
                {
                    Monitor.Wait(_resultLock);
                }
            }
        }

        public void SetResult()
        {
            lock (_resultLock)
            {
                if (!ResultEmitted)
                {
                    ResultEmitted = true;
                    Monitor.Pulse(_resultLock);
                }
            }
        }

        public void Terminate()
        {
            lock (_queueLock)
            {
                if (!Terminated)
                {
                    Terminated = true;
                    Monitor.Pulse(_queueLock);
                }
            }
        }

        public void Start()
        {
            while (!Terminated)
            {
                MessageEnvelope msg;
                lock (_queueLock)
                {
                    while (_queue.IsEmpty && !Terminated)
                    {
                        Monitor.Wait(_queueLock);
                    }

                    if (Terminated)
                        return;

                    _queue.TryDequeue(out msg);
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