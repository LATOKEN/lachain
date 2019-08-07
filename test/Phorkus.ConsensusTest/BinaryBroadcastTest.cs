//using System;
//using System.Collections.Generic;
//using System.Linq;
//using NUnit.Framework;
//using Phorkus.Consensus;
//using Phorkus.Consensus.BinaryAgreement;
//using Phorkus.Proto;
//using Phorkus.Utility.Utils;
//
//namespace Phorkus.ConsensusTest
//{
//    [TestFixture]
//    public class BinaryBroadcastTest
//    {
//        private IConsensusProtocol[] _broadcasts;
//        private const int N = 7;
//        private const int F = 2;
//
//        [SetUp]
//        public void SetUp()
//        {
//            _broadcasts = new IConsensusProtocol[N];
//            for (uint i = 0; i < N; ++i)
//            {
//                var broadcastSimulator = new BroadcastSimulator(i, this);
//                _broadcasts[i] = new BinaryBroadcast(N, F, new BinaryBroadcastId(0, 0, 0), broadcastSimulator);
//            }
//        }
//
//        [Test]
//        public void TestBinaryBroadcastAllZero()
//        {
//            var received = new int[N];
//            for (var i = 0; i < N; ++i)
//            {
//                _broadcasts[i].Input(false);
//            }
//
//            for (var i = 0; i < N; ++i)
//            {
//                var validator = i;
//                Assert.IsTrue(_broadcasts[i].Terminated(out var res), "Protocol must be terminated");
//                Assert.AreEqual(new BoolSet(false), res);
//                received[validator]++;
//            }
//            Assert.IsTrue(received.All(v => v == 1));
//        }
//
//        [Test]
//        public void TestBinaryBroadcastAllOne()
//        {
//            var received = new int[N];
//
//            for (var i = 0; i < N; ++i)
//                _broadcasts[i].Input(true);
//
//            for (var i = 0; i < N; ++i)
//            {
//                Assert.IsTrue(_broadcasts[i].Terminated(out var values), "Protocol must be terminated");
//                Assert.AreEqual(new BoolSet(true), values);
//                received[i]++;
//            }
//
//            Assert.IsTrue(received.All(v => v == 1));
//        }
//
//        [Test]
//        public void TestRandomFailures()
//        {
//            var random = new Random();
//            var cnt = 0;
//            while (cnt < F)
//            {
//                var x = random.Next() % N;
//                if (_broadcasts[x] == null) continue;
//                _broadcasts[x] = null;
//                ++cnt;
//            }
//
//            for (var i = 0; i < N; ++i)
//                _broadcasts[i]?.Input(true);
//
//            var received = new int[N];
//            for (var i = 0; i < N; ++i)
//            {
//                if (_broadcasts[i] == null) continue;
//                Assert.IsTrue(_broadcasts[i].Terminated(out var values), "Protocol must be terminated"); 
//                Assert.AreEqual(new BoolSet(true), values);
//                received[i]++;
//            }
//            Assert.IsTrue(received.Zip(_broadcasts, (v, broadcast) => broadcast == null || v == 1).All(b => b));
//        }
//
//        [Test]
//        [Repeat(100)]
//        public void TestRandomValues()
//        {
//            var random = new Random();
//            var cnt = 0;
//            while (cnt < F)
//            {
//                var x = random.Next() % N;
//                if (_broadcasts[x] == null) continue;
//                _broadcasts[x] = null;
//                ++cnt;
//            }
//
//
//            var inputs = new int[N];
//            int[] inputsCount = {0, 0};
//            for (var i = 0; i < N; ++i)
//            {
//                if (_broadcasts[i] == null) continue;
//                inputs[i] = random.Next() % 2;
//                inputsCount[inputs[i]]++;
//            }
//
//            for (var i = 0; i < N; ++i)
//            {
//                _broadcasts[i]?.Input(inputs[i] == 1);
//            }
//
//            var received = new ISet<int>[N];
//            for (var i = 0; i < N; ++i)
//            {
//                if (_broadcasts[i] == null) continue;
//                received[i] = new SortedSet<int>();
//                Assert.IsTrue(_broadcasts[i].Terminated(out var values), "Protocol must be terminated");
//                foreach (var b in values.Values())
//                    received[i].Add(b ? 1 : 0);
//            }
//
//            ISet<int> firstReceived = null;
//            for (var i = 0; i < N; ++i)
//            {
//                if (_broadcasts[i] == null) continue;
//                if (firstReceived == null) firstReceived = received[i];
//                for (var v = 0; v < 2; ++v)
//                {
//                    if (inputsCount[v] < F + 1) continue;
//                    Assert.Contains(v, received[i].ToList(),
//                        "all correct nodes should output value if at least F + 1 inputed it");
//                }
//
//                Assert.Greater(received[i].Count, 0, "all correct nodes should output something");
//                Assert.IsTrue(firstReceived.SequenceEqual(received[i]), "all correct nodes should output same values");
//            }
//        }
//
//        private class CorruptedBroadcastSimulator : IConsensusBroadcaster
//        {
//            private readonly uint _sender;
//            private readonly BinaryBroadcastTest _test;
//            private readonly Random _random;
//
//            public CorruptedBroadcastSimulator(uint sender, BinaryBroadcastTest test, Random random)
//            {
//                _sender = sender;
//                _test = test;
//                _random = random;
//            }
//
//            public void Broadcast(ConsensusMessage message)
//            {
//                switch (message.PayloadCase)
//                {
//                    case ConsensusMessage.PayloadOneofCase.Bval:
//                        message.Bval.Value = _random.Next() % 2 == 1;
//                        break;
//                    case ConsensusMessage.PayloadOneofCase.Aux:
//                        message.Aux.Value = _random.Next() % 2 == 1;
//                        break;
//                    default:
//                        throw new ArgumentException(nameof(message) + " is carrying type " + message.PayloadCase);
//                }
//
//                message.Validator.ValidatorIndex = _sender;
//                for (var i = 0; i < N; ++i)
//                {
//                    _test._broadcasts[i]?.ReceiveMessage(message);
//                }
//            }
//
//            public void MessageSelf(InternalMessage message)
//            {
//                _test._broadcasts[_sender].HandleInternalMessage(message);
//            }
//
//            public uint GetMyId()
//            {
//                return _sender;
//            }
//        }
//
//        [Test]
//        [Repeat(100)]
//        public void TestRandomBroadcastCorruptions()
//        {
//            var random = new Random();
//            var cnt = 0;
//            var corrupted = new bool[N];
//            while (cnt < F)
//            {
//                var x = random.Next() % N;
//                if (corrupted[x]) continue;
//                var broadcastSimulator = new CorruptedBroadcastSimulator((uint) x, this, random);
//                _broadcasts[x] = new BinaryBroadcast(N, F, new BinaryBroadcastId(0, 0, 0), broadcastSimulator);
//                corrupted[x] = true;
//                ++cnt;
//            }
//
//            var inputs = new int[N];
//            int[] inputsCount = {0, 0};
//            for (var i = 0; i < N; ++i)
//            {
//                if (corrupted[i]) continue;
//                inputs[i] = random.Next() % 2;
//                inputsCount[inputs[i]]++;
//            }
//
//            for (var i = 0; i < N; ++i)
//            {
//                _broadcasts[i]?.Input(inputs[i] == 1);
//            }
//            
//            var received = new ISet<int>[N];
//            for (var i = 0; i < N; ++i)
//            {
//                if (corrupted[i]) continue;
//                received[i] = new SortedSet<int>();
//                Assert.IsTrue(_broadcasts[i].Terminated(out var values), "Protocol must be terminated");
//                foreach (var b in values.Values())
//                    received[i].Add(b ? 1 : 0);
//            }
//
//            ISet<int> firstReceived = null;
//            for (var i = 0; i < N; ++i)
//            {
//                if (corrupted[i]) continue;
//                if (firstReceived == null) firstReceived = received[i];
//                Assert.Greater(received[i].Count, 0, "all correct nodes should output something");
//            }
//        }
//    }
//}