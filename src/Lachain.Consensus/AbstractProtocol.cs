﻿using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using Lachain.Logger;
using Lachain.Consensus.Messages;
using Lachain.Proto;
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
        public bool Started { get; private set; } = false;
        
        // waiting for 10 block time
        // TODO: calculate _requestTime properly
        private const ulong _requestTime = 5 * 4 * 1000;

        protected AbstractProtocol(
            IPublicConsensusKeySet wallet,
            IProtocolIdentifier id,
            IConsensusBroadcaster broadcaster
        )
        {
            _thread = new Thread(Start) {IsBackground = true};
            Broadcaster = broadcaster;
            Id = id;
            Wallet = wallet;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void StartThread()
        {
            if (Started)
            {
                throw new InvalidOperationException("StartThread() already called previously");
            }

            _thread.Start();
            Started = true;
        }

        public bool HasThreadStarted()
        {
            return Started;
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
                // We can empty the _queue because the messages will no longer be processed
                // This will free some memory in case of spam messages
                _queue.Clear();
                Monitor.Pulse(_queueLock);
            }
        }

        public void Start()
        {
            _startTime = TimeUtils.CurrentTimeMillis();
            var lastRequestTime = _startTime;
            while (!Terminated)
            {
                MessageEnvelope msg;
                lock (_queueLock)
                {
                    while (_queue.IsEmpty && !Terminated)
                    {
                        var queueUsed = Monitor.Wait(_queueLock, 1000);
                        if (TimeUtils.CurrentTimeMillis() - _startTime > _alertTime)
                        {
                            Logger.LogWarning($"Protocol {Id} is waiting for _queueLock too long, last message" +
                                              $" is [{_lastMessage}]");
                        }
                        if (!queueUsed && TimeUtils.CurrentTimeMillis() - lastRequestTime > _requestTime)
                        {
                            _protocolWaitingTooLong?.Invoke(this, Id);
                            lastRequestTime = TimeUtils.CurrentTimeMillis();
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
                    // We should investigate exceptions for each protocol and handle them carefully. Consensus depend 
                    // on the messages from honest validators to deliver properly, no matter the delay. If a protocol
                    // stops due to exception while handling message from malicious node, this may interrupt the communication
                    // among honest nodes which may cause the consensus stop working, or worse, giving wrong result.
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
                if (Terminated)
                {
                    // we should return here instead of enqueueing messages
                    // because once terminated, the messages are not being processed anymore
                    // so the queue will just get large unnecessarily
                    Monitor.Pulse(_queueLock);
                    return;
                }
                _queue.Enqueue(message);
                Monitor.Pulse(_queueLock);
            }
        }

        protected void InvokeReceivedExternalMessage(int from, ConsensusMessage msg)
        {
            // received a valid msg from validator
            _receivedExternalMessage?.Invoke(this, (from, msg));
        }

        protected void InvokeMessageBroadcasted(ConsensusMessage msg)
        {
            // an external message is broadcasted
            _messageBroadcasted?.Invoke(this, msg);
        }

        protected void InvokeMessageSent(int validator, ConsensusMessage msg)
        {
            // an external message is sent to validator
            _messageSent?.Invoke(this, (validator, msg));
        }

        public abstract void ProcessMessage(MessageEnvelope envelope);

        public event EventHandler<IProtocolIdentifier>? _protocolWaitingTooLong;
        public event EventHandler<(int from, ConsensusMessage msg)>? _receivedExternalMessage;
        public event EventHandler<ConsensusMessage>? _messageBroadcasted;
        public event EventHandler<(int validator, ConsensusMessage msg)>? _messageSent;
    }
}