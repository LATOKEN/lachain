using System;
using System.Collections.Concurrent;
using System.Threading;
using Lachain.Logger;
using Lachain.Consensus.Messages;
using Lachain.Utility.Utils;
using Prometheus;

namespace Lachain.Consensus
{
    public abstract class AbstractProtocol : IConsensusProtocol
    {
        private static readonly ILogger<AbstractProtocol> Logger = LoggerFactory.GetLoggerForClass<AbstractProtocol>();

        private static readonly Counter MessageCounter = Metrics.CreateCounter(
            "lachain_consensus_messages_processed",
            "Number of messages processed by protocol and message type",
            new CounterConfiguration
            {
                LabelNames = new[] {"protocol", "message_type"}
            }
        );

        private readonly ConcurrentQueue<MessageEnvelope> _queue = new ConcurrentQueue<MessageEnvelope>();
        private readonly object _queueLock = new object();
        private readonly object _resultLock = new object();
        private bool ResultEmitted { get; set; }
        public bool Terminated { get; protected set; }
        public IProtocolIdentifier Id { get; }
        protected readonly IConsensusBroadcaster Broadcaster;
        private readonly Thread _thread;
        protected readonly IPublicConsensusKeySet Wallet;
        protected int N => Wallet.N;
        protected int F => Wallet.F;

        protected string _lastMessage = "";
        private ulong _startTime = 0;
        private const ulong _alertTime = 60 * 1000;

        protected AbstractProtocol(
            IPublicConsensusKeySet wallet,
            IProtocolIdentifier id,
            IConsensusBroadcaster broadcaster
        )
        {
            _thread = new Thread(Start) {IsBackground = true};
            _thread.Start();
            Broadcaster = broadcaster;
            Id = id;
            Wallet = wallet;
        }

        public int GetMyId()
        {
            return Broadcaster.GetMyId();
        }

        public void WaitFinish()
        {
            _thread.Join();
        }

        public bool WaitFinish(TimeSpan timeout)
        {
            return _thread.Join(timeout);
        }

        public void WaitResult()
        {
            lock (_resultLock)
            {
                if (ResultEmitted) return;
                while (!ResultEmitted)
                {
                    Monitor.Wait(_resultLock);
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
                Logger.LogTrace($"{Id}: protocol is terminated");
                Terminated = true;
                Monitor.Pulse(_queueLock);
            }
        }

        public void Start()
        {
            _startTime = TimeUtils.CurrentTimeMillis();
            while (!Terminated)
            {
                MessageEnvelope msg;
                lock (_queueLock)
                {
                    while (_queue.IsEmpty && !Terminated)
                    {
                        Monitor.Wait(_queueLock, 1000);
                        if (TimeUtils.CurrentTimeMillis() - _startTime > _alertTime)
                        {
                            Logger.LogWarning($"Protocol {Id} is waiting for _queueLock too long, last message" +
                                              $" is [{_lastMessage}]");
                        }
                    }

                    if (Terminated)
                        return;

                    _queue.TryDequeue(out msg);
                }

                try
                {
                    ProcessMessage(msg);
                    MessageCounter.WithLabels($"{Id}".Split(' ')[0], msg.TypeString()).Inc();
                    if (TimeUtils.CurrentTimeMillis() - _startTime > _alertTime)
                    {
                        Logger.LogWarning($"Protocol {Id} is too long, last message is [{_lastMessage}]");
                    }
                }
                catch (Exception e)
                {
                    Logger.LogError($"{Id}: exception occured while processing message: {e}");
                    Terminated = true;
                    break;
                }
            }
        }

        public void ReceiveMessage(MessageEnvelope message)
        {
            lock (_queueLock)
            {
                if (Terminated){}
            //        Logger.LogTrace($"{Id}: got message after termination"); 
                _queue.Enqueue(message);
                Monitor.Pulse(_queueLock);
            }
        }

        public abstract void ProcessMessage(MessageEnvelope envelope);
    }
}