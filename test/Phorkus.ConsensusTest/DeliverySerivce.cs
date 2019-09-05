using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Org.BouncyCastle.Crypto.Engines;
using Phorkus.Consensus;
using Phorkus.Consensus.Messages;
using Phorkus.Proto;

namespace Phorkus.ConsensusTest
{
    public class DeliverySerivce
    {
//        private readonly List<IConsensusBroadcaster> _broadcasters = new List<IConsensusBroadcaster>();
        private readonly IDictionary<int, IConsensusBroadcaster> _broadcasters = new Dictionary<int, IConsensusBroadcaster>();
//        private readonly ConcurrentQueue<Tuple<int, ConsensusMessage>> _queue = new ConcurrentQueue<Tuple<int, ConsensusMessage>>();
        private readonly RandomSamplingQueue<Tuple<int, ConsensusMessage>> _queue = new RandomSamplingQueue<Tuple<int, ConsensusMessage>>();
        private readonly object _queueLock = new object();
        private bool Terminated { get; set; }

        private readonly Thread _thread;

        public DeliverySerivce()
        {
            _thread = new Thread(Start) {IsBackground = true};
            _thread.Start();
        }
        
        // todo replace array with map and provide index to this function
        public void AddPlayer(int index, IConsensusBroadcaster player)
        {
            _broadcasters.Add(index, player);
        }

        
        public void WaitFinish()
        {
            Terminated = true;
            lock (_queueLock)
            {
                Monitor.Pulse(_queueLock);
            }
            _thread.Join();
        }

        private void Start()
        {
            while (!Terminated)
            {
                Tuple<int, ConsensusMessage> tuple;
                lock (_queueLock) 
                {
                    while (_queue.IsEmpty && !Terminated)
                    {
                        Monitor.Wait(_queueLock);
                    }
                    
                    if (Terminated)
                        return;
                    
                    if (!_queue.TryTakeLast(out tuple))
//                    if (!_queue.TryDequeue(out tuple))
//                    if (!_queue.TrySample(out tuple))
                    {
                        throw new Exception("Can sample from queue!");
                    }

                    Console.Error.WriteLine($"remaining in queue: {_queue.Count}");
                }


//                _queue.TryDequeue(out var tuple);
//                _queue.TrySample(out var tuple);
//                _queue.TryTakeLast(out var tuple);
                
                var index = tuple.Item1;
                var message = tuple.Item2;
                
                try
                {
                    _broadcasters[index].Dispatch(message);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e);
                    Terminated = true;
                    break;
                }
                
            }
        }

        private void ReceiveMessage(int index, ConsensusMessage message)
        {
            lock (_queueLock)
            {
                _queue.Enqueue(new Tuple<int, ConsensusMessage>(index, message));
                Monitor.Pulse(_queueLock);
            }
        }

        public void BroadcastMessage(ConsensusMessage consensusMessage)
        {
            for (var i = 0; i < _broadcasters.Count; ++i)
            {
                ReceiveMessage(i, consensusMessage);
            }
        }

        public void SendToPlayer(int index, ConsensusMessage consensusMessage)
        {
            ReceiveMessage(index, consensusMessage);
        }
    }
}