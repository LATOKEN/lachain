using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Phorkus.Consensus;
using Phorkus.Consensus.BinaryAgreement;
using Phorkus.Crypto.MCL.BLS12_381;
using Phorkus.Proto;
using BinaryBroadcast = Phorkus.Consensus.BinaryAgreement.BinaryBroadcast;

namespace Phorkus.ConsensusTest
{
    [TestFixture]
    public class BinaryBroadcastTest
    {
        private IBinaryBroadcast[] _broadcasts;
        private const int N = 7;
        private const int F = 2;

        private class BroadcastSimulator : IConsensusBroadcaster
        {
            private readonly uint _sender;
            private readonly BinaryBroadcastTest _test;

            public BroadcastSimulator(uint sender, BinaryBroadcastTest test)
            {
                _sender = sender;
                _test = test;
            }

            public void Broadcast(ConsensusMessage message)
            {
                if (!message.PayloadCase.Equals(ConsensusMessage.PayloadOneofCase.BinaryBroadcast))
                    throw new ArgumentException(nameof(message));
                message.Validator.ValidatorIndex = _sender;
                for (var i = 0; i < N; ++i)
                {
                    _test._broadcasts[i]?.HandleMessage(message);
                }
            }
        }

        [SetUp]
        public void SetUp()
        {
            _broadcasts = new IBinaryBroadcast[N];
            for (uint i = 0; i < N; ++i)
            {
                var broadcastSimulator = new BroadcastSimulator(i, this);
                _broadcasts[i] = new BinaryBroadcast(N, F, new BinaryBroadcastId(0, 0, 0), broadcastSimulator);
            }
        }

        [Test]
        public void TestBinaryBroadcastAllZero()
        {
            var received = new int[N];
            for (var i = 0; i < N; ++i)
            {
                var validator = i;
                _broadcasts[i].BinValueAdded += (sender, value) =>
                {
                    Assert.AreEqual(value, 0);
                    received[validator]++;
                };
            }

            for (var i = 0; i < N; ++i)
            {
                _broadcasts[i].Input(0);
            }

            Assert.IsTrue(received.All(v => v == 1));
        }

        [Test]
        public void TestBinaryBroadcastAllOne()
        {
            var received = new int[N];
            for (var i = 0; i < N; ++i)
            {
                var validator = i;
                _broadcasts[i].BinValueAdded += (sender, value) =>
                {
                    Assert.AreEqual(value, 1);
                    received[validator]++;
                };
            }

            for (var i = 0; i < N; ++i)
            {
                _broadcasts[i].Input(1);
            }

            Assert.IsTrue(received.All(v => v == 1));
        }

        [Test]
        public void TestRandomFailures()
        {
            var random = new Random();
            var cnt = 0;
            while (cnt < F)
            {
                var x = random.Next() % N;
                if (_broadcasts[x] == null) continue;
                _broadcasts[x] = null;
                ++cnt;
            }

            var received = new int[N];
            for (var i = 0; i < N; ++i)
            {
                if (_broadcasts[i] == null) continue;
                var validator = i;
                _broadcasts[i].BinValueAdded += (sender, value) =>
                {
                    Assert.AreEqual(value, 1);
                    received[validator]++;
                };
            }

            for (var i = 0; i < N; ++i)
            {
                _broadcasts[i]?.Input(1);
            }

            Assert.IsTrue(received.Zip(_broadcasts, (v, broadcast) => broadcast == null || v == 1).All(b => b));
        }

        [Test]
        [Repeat(100)]
        public void TestRandomValues()
        {
            var random = new Random();
            var cnt = 0;
            while (cnt < F)
            {
                var x = random.Next() % N;
                if (_broadcasts[x] == null) continue;
                _broadcasts[x] = null;
                ++cnt;
            }

            var received = new ISet<int>[N];
            for (var i = 0; i < N; ++i)
            {
                if (_broadcasts[i] == null) continue;
                received[i] = new SortedSet<int>();
                var validator = i;
                _broadcasts[i].BinValueAdded += (sender, value) =>
                {
                    Assert.Contains(value, new[] {0, 1});
                    received[validator].Add(value);
                };
            }

            var inputs = new int[N];
            int[] inputsCount = {0, 0};
            for (var i = 0; i < N; ++i)
            {
                if (_broadcasts[i] == null) continue;
                inputs[i] = random.Next() % 2;
                inputsCount[inputs[i]]++;
            }

            for (var i = 0; i < N; ++i)
            {
                _broadcasts[i]?.Input(inputs[i]);
            }

            ISet<int> firstRecieved = null;
            for (var i = 0; i < N; ++i)
            {
                if (_broadcasts[i] == null) continue;
                if (firstRecieved == null) firstRecieved = received[i];
                for (var v = 0; v < 2; ++v)
                {
                    if (inputsCount[v] < F + 1) continue;
                    Assert.Contains(v, received[i].ToList(),
                        "all correct nodes should output value if at least F + 1 inputed it");
                }

                Assert.Greater(received[i].Count, 0, "all correct nodes should output something");
                Assert.IsTrue(firstRecieved.SequenceEqual(received[i]), "all correct nodes should output same values");
            }
        }
        
        private class CorruptedBroadcastSimulator : IConsensusBroadcaster
        {
            private readonly uint _sender;
            private readonly BinaryBroadcastTest _test;
            private readonly Random _random;

            public CorruptedBroadcastSimulator(uint sender, BinaryBroadcastTest test, Random random)
            {
                _sender = sender;
                _test = test;
                _random = random;
            }

            public void Broadcast(ConsensusMessage message)
            {
                if (!message.PayloadCase.Equals(ConsensusMessage.PayloadOneofCase.BinaryBroadcast))
                    throw new ArgumentException(nameof(message));
                message.Validator.ValidatorIndex = _sender;
                message.BinaryBroadcast.Value = _random.Next() % 2 == 1;
                for (var i = 0; i < N; ++i)
                {
                    _test._broadcasts[i]?.HandleMessage(message);
                }
            }
        }

        [Test]
        [Repeat(100)]
        public void TestRandomBroadcastCorruptions()
        {
            var random = new Random();
            var cnt = 0;
            var corrupted = new bool[N];
            while (cnt < F)
            {
                var x = random.Next() % N;
                if (corrupted[x]) continue;
                var broadcastSimulator = new CorruptedBroadcastSimulator((uint) x, this, random);
                _broadcasts[x] = new BinaryBroadcast(N, F, new BinaryBroadcastId(0, 0, 0), broadcastSimulator);
                corrupted[x] = true;
                ++cnt;
            }

            var received = new ISet<int>[N];
            for (var i = 0; i < N; ++i)
            {
                if (corrupted[i]) continue;
                received[i] = new SortedSet<int>();
                var validator = i;
                _broadcasts[i].BinValueAdded += (sender, value) =>
                {
                    Assert.Contains(value, new[] {0, 1});
                    received[validator].Add(value);
                };
            }

            var inputs = new int[N];
            int[] inputsCount = {0, 0};
            for (var i = 0; i < N; ++i)
            {
                if (corrupted[i]) continue;
                inputs[i] = random.Next() % 2;
                inputsCount[inputs[i]]++;
            }

            for (var i = 0; i < N; ++i)
            {
                _broadcasts[i]?.Input(inputs[i]);
            }

            ISet<int> firstRecieved = null;
            for (var i = 0; i < N; ++i)
            {
                if (corrupted[i]) continue;
                if (firstRecieved == null) firstRecieved = received[i];
                for (var v = 0; v < 2; ++v)
                {
                    if (inputsCount[v] < F + 1) continue;
                    Assert.Contains(v, received[i].ToList(),
                        "all correct nodes should output value if at least F + 1 inputed it");
                }

                Assert.Greater(received[i].Count, 0, "all correct nodes should output something");
                Assert.IsTrue(firstRecieved.SequenceEqual(received[i]), "all correct nodes should output same values");
            }
        }
    }
}