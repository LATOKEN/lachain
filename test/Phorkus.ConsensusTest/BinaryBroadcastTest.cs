using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Phorkus.Consensus;
using Phorkus.Consensus.BinaryAgreement;
using Phorkus.Crypto.MCL.BLS12_381;
using Phorkus.Proto;
using Phorkus.Utility.Utils;
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
                if (!message.PayloadCase.Equals(ConsensusMessage.PayloadOneofCase.BinaryBroadcast) &&
                    !message.PayloadCase.Equals(ConsensusMessage.PayloadOneofCase.Aux))
                    throw new ArgumentException(nameof(message) + " is carrying type " + message.PayloadCase);
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
                _broadcasts[i].ValuesDecided += (sender, values) =>
                {
                    Assert.AreEqual(new BoolSet(false), values);
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
                _broadcasts[i].ValuesDecided += (sender, values) =>
                {
                    Assert.AreEqual(new BoolSet(true), values);
                    received[validator]++;
                };
            }

            for (var i = 0; i < N; ++i)
                _broadcasts[i].Input(1);

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
                _broadcasts[i].ValuesDecided += (sender, values) =>
                {
                    Assert.AreEqual(new BoolSet(true), values);
                    received[validator]++;
                };
            }

            for (var i = 0; i < N; ++i)
                _broadcasts[i]?.Input(1);

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
                _broadcasts[i].ValuesDecided += (sender, value) =>
                {
                    foreach (var b in value.Values())
                        received[validator].Add(b ? 1 : 0);
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

            ISet<int> firstReceived = null;
            for (var i = 0; i < N; ++i)
            {
                if (_broadcasts[i] == null) continue;
                if (firstReceived == null) firstReceived = received[i];
                for (var v = 0; v < 2; ++v)
                {
                    if (inputsCount[v] < F + 1) continue;
                    Assert.Contains(v, received[i].ToList(),
                        "all correct nodes should output value if at least F + 1 inputed it");
                }

                Assert.Greater(received[i].Count, 0, "all correct nodes should output something");
                Assert.IsTrue(firstReceived.SequenceEqual(received[i]), "all correct nodes should output same values");
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
                switch (message.PayloadCase)
                {
                    case ConsensusMessage.PayloadOneofCase.BinaryBroadcast:
                        message.BinaryBroadcast.Value = _random.Next() % 2 == 1;
                        break;
                    case ConsensusMessage.PayloadOneofCase.Aux:
                        message.Aux.Value = _random.Next() % 2 == 1;
                        break;
                    default:
                        throw new ArgumentException(nameof(message) + " is carrying type " + message.PayloadCase);
                }

                message.Validator.ValidatorIndex = _sender;
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
                _broadcasts[i].ValuesDecided += (sender, values) =>
                {
                    foreach (var b in values.Values())
                        received[validator].Add(b ? 1 : 0);
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

            ISet<int> firstReceived = null;
            for (var i = 0; i < N; ++i)
            {
                if (corrupted[i]) continue;
                if (firstReceived == null) firstReceived = received[i];
                Assert.Greater(received[i].Count, 0, "all correct nodes should output something");
            }
        }
    }
}