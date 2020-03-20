using System;
using System.Collections.Generic;
using System.Threading;
using Lachain.Consensus;
using Lachain.Proto;

namespace Lachain.ConsensusTest
{
    public class DeliveryService
    {
        public readonly ISet<int> _mutedPlayers = new HashSet<int>();
        private readonly IDictionary<int, IConsensusBroadcaster> _broadcasters = new Dictionary<int, IConsensusBroadcaster>();
        private readonly RandomSamplingQueue<Tuple<int, int, ConsensusMessage>> _queue = new RandomSamplingQueue<Tuple<int, int, ConsensusMessage>>();
        private readonly object _queueLock = new object();
        public double RepeatProbability
        {
            get => _queue.RepeatProbability;
            set => _queue.RepeatProbability = value;
        }

        private bool Terminated { get; set; }

        private readonly Thread _thread;
        private bool _stopped;
        public DeliveryServiceMode Mode { get; set; }

        public DeliveryService()
        {
            Mode = DeliveryServiceMode.TAKE_FIRST;
            _thread = new Thread(Start) {IsBackground = true};
            _thread.Start();
        }
        
        public void AddPlayer(int index, IConsensusBroadcaster player)
        {
            _broadcasters.Add(index, player);
        }

        public void MutePlayer(int index)
        {
            _mutedPlayers.Add(index);
        }
        
        public void WaitFinish()
        {
            Terminated = true;
            lock (_queueLock)
            {
                Monitor.Pulse(_queueLock);
                Console.Error.WriteLine("_queueLock pulsed.");
            }
            _thread.Join();
        }

        private void Start()
        {
            while (!Terminated || !_queue.IsEmpty)
            {
                lock (_queueLock)
                {
                    while (_queue.IsEmpty && !Terminated)
                    {
                        Monitor.Wait(_queueLock);
                    }

                    if (!_queue.IsEmpty)
                    {
                        Tuple<int, int, ConsensusMessage> tuple;
                        var success = Mode switch
                        {
                            DeliveryServiceMode.TAKE_FIRST => _queue.TryDequeue(out tuple),
                            DeliveryServiceMode.TAKE_LAST => _queue.TryTakeLast(out tuple),
                            DeliveryServiceMode.TAKE_RANDOM => _queue.TrySample(out tuple),
                            _ => throw new NotImplementedException($"Unknown mode {Mode}")
                        };

                        if (!success)
                        {
                            throw new Exception("Can't sample from queue!");
                        }

                        Console.Error.WriteLine($"remaining in queue: {_queue.Count}");

                        var from = tuple.Item1;
                        var index = tuple.Item2;
                        var message = tuple.Item3;
                        
                        if (_mutedPlayers.Contains(index)) continue;
                        
                        try
                        {
                            _broadcasters[index].Dispatch(message, from);
                        }
                        catch (Exception e)
                        {
                            Console.Error.WriteLine(e);
                            Terminated = true;
                            break;
                        }
                    }
                    else
                    {
                        if (Terminated) return;
                    }
                }
            }
        }

        private void ReceiveMessage(int from, int to, ConsensusMessage message)
        {
            if (Terminated) return;
            lock (_queueLock)
            {
                _queue.Enqueue(new Tuple<int, int, ConsensusMessage>(from, to, message));
                Monitor.Pulse(_queueLock);
            }
        }

        public void BroadcastMessage(int from, ConsensusMessage consensusMessage)
        {
            for (var i = 0; i < _broadcasters.Count; ++i)
            {
                ReceiveMessage(from, i, consensusMessage);
            }
        }

        public void SendToPlayer(int from, int index, ConsensusMessage consensusMessage)
        {
            ReceiveMessage(from, index, consensusMessage);
        }
    }
}