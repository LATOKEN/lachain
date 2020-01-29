using System;
using System.Collections.Concurrent;
using System.Threading;
using Phorkus.Consensus.Messages;
using Phorkus.Logger;

namespace Phorkus.Consensus
{
    public abstract class AbstractProtocol : IConsensusProtocol
    {
        private readonly ConcurrentQueue<MessageEnvelope> _queue = new ConcurrentQueue<MessageEnvelope>();
        private readonly object _queueLock = new object();
        private readonly object _resultLock = new object();
        private bool ResultEmitted { get; set; }
        public bool Terminated { get; protected set; }
        public IProtocolIdentifier Id { get; }
        protected readonly IConsensusBroadcaster Broadcaster;
        private readonly Thread _thread;
        protected readonly IWallet Wallet;
        protected int N => Wallet.N;
        protected int F => Wallet.F;
        private readonly ILogger<AbstractProtocol> _logger = LoggerFactory.GetLoggerForClass<AbstractProtocol>();

        protected AbstractProtocol(IWallet wallet, IProtocolIdentifier id, IConsensusBroadcaster broadcaster)
        {
            _thread = new Thread(Start) {IsBackground = true};
            _thread.Start();
            Broadcaster = broadcaster;
            Id = id;
            Wallet = wallet;
            Wallet.ProtocolIds.Add(Id);
        }

        public int GetMyId()
        {
            return Broadcaster.GetMyId();
        }

        public void WaitFinish()
        {
            _thread.Join();
        }

        public void WaitResult()
        {
            lock (_resultLock)
            {
                if (ResultEmitted) return;
                Monitor.Wait(_resultLock);
                if (!ResultEmitted)
                {
                    throw new Exception("Should set ResultEmitted to true before pulse.");
                }
            }
        }

        public void SetResult()
        {
            lock (_resultLock)
            {
                if (ResultEmitted) return;
                ResultEmitted = true;
                Monitor.Pulse(_resultLock);
            }
        }

        public void Terminate()
        {
            lock (_queueLock)
            {
                if (Terminated) return;
                Terminated = true;
                Monitor.Pulse(_queueLock);
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
                    _logger.LogDebug(e, "Exception occured while processing protocol message");
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