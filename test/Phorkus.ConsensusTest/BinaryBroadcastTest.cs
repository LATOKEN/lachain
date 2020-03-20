using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Phorkus.Consensus;
using Phorkus.Consensus.BinaryAgreement;
using Phorkus.Consensus.Messages;
using Phorkus.Crypto.MCL.BLS12_381;
using Phorkus.Proto;
using Phorkus.Utility.Utils;

namespace Phorkus.ConsensusTest
{
    [TestFixture]
    public class BinaryBroadcastTest
    {
        [SetUp]
        public void SetUp()
        {
            Mcl.Init();
            _deliveryService = new DeliveryService();
            _broadcasts = new IConsensusProtocol[N];
            _broadcasters = new IConsensusBroadcaster[N];
            _resultInterceptors = new ProtocolInvoker<BinaryBroadcastId, BoolSet>[N];
            _privateKeys = new IPrivateConsensusKeySet[N];
            _publicKeys = new PublicConsensusKeySet(
                N, F, null, null,
                null, Enumerable.Empty<ECDSAPublicKey>()
            );
            for (var i = 0; i < N; ++i)
            {
                _resultInterceptors[i] = new ProtocolInvoker<BinaryBroadcastId, BoolSet>();
                _privateKeys[i] = TestUtils.EmptyWallet(N, F);
                _broadcasters[i] = new BroadcastSimulator(i, _publicKeys, _privateKeys[i], _deliveryService, false);
            }
        }

        private DeliveryService _deliveryService;
        private IConsensusProtocol[] _broadcasts;
        private IConsensusBroadcaster[] _broadcasters;
        private ProtocolInvoker<BinaryBroadcastId, BoolSet>[] _resultInterceptors;
        private const int N = 7;
        private const int F = 2;
        private IPrivateConsensusKeySet[] _privateKeys;
        private IPublicConsensusKeySet _publicKeys;

        private void SetUpAllHonest()
        {
            for (uint i = 0; i < N; ++i)
            {
                _broadcasts[i] = new BinaryBroadcast(new BinaryBroadcastId(0, 0, 0), _publicKeys, _broadcasters[i]);
                _broadcasters[i].RegisterProtocols(new[] {_broadcasts[i], _resultInterceptors[i]});
            }
        }

        private void SetupSomeSilent()
        {
            var random = new Random();
            var cnt = 0;
            while (cnt < F)
            {
                var x = random.Next() % N;
                if (_broadcasts[x] != null) continue;
                _broadcasts[x] = new SilentProtocol<BinaryBroadcastId>(new BinaryBroadcastId(0, 0, 0));
                ++cnt;
            }

            for (uint i = 0; i < N; ++i)
            {
                if (_broadcasts[i] == null)
                    _broadcasts[i] = new BinaryBroadcast(new BinaryBroadcastId(0, 0, 0), _publicKeys, _broadcasters[i]);
                _broadcasters[i].RegisterProtocols(new[] {_broadcasts[i], _resultInterceptors[i]});
            }
        }

        [Test]
        public void TestBinaryBroadcastAllOne()
        {
            SetUpAllHonest();
            for (var i = 0; i < N; ++i)
                _broadcasters[i].InternalRequest(new ProtocolRequest<BinaryBroadcastId, bool>(
                    _resultInterceptors[i].Id, _broadcasts[i].Id as BinaryBroadcastId, true
                ));

            for (var i = 0; i < N; ++i) _broadcasts[i].WaitFinish();

            for (var i = 0; i < N; ++i)
            {
                Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminated");
                Assert.AreEqual(_resultInterceptors[i].ResultSet, 1, $"protocol {i} emitted result not once");
                Assert.AreEqual(new BoolSet(true), _resultInterceptors[i].Result);
            }
        }

        [Test]
        public void TestBinaryBroadcastAllZero()
        {
            SetUpAllHonest();
            for (var i = 0; i < N; ++i)
                _broadcasters[i].InternalRequest(new ProtocolRequest<BinaryBroadcastId, bool>(
                    _resultInterceptors[i].Id, _broadcasts[i].Id as BinaryBroadcastId, false
                ));

            for (var i = 0; i < N; ++i) _broadcasts[i].WaitFinish();

            for (var i = 0; i < N; ++i)
            {
                Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminated");
                Assert.AreEqual(_resultInterceptors[i].ResultSet, 1, $"protocol {i} emitted result not once");
                Assert.AreEqual(new BoolSet(false), _resultInterceptors[i].Result);
            }
        }

        [Test]
        public void TestRandomFailures()
        {
            SetupSomeSilent();

            for (var i = 0; i < N; ++i)
                _broadcasters[i].InternalRequest(new ProtocolRequest<BinaryBroadcastId, bool>(
                    _resultInterceptors[i].Id, _broadcasts[i].Id as BinaryBroadcastId, true
                ));

            for (var i = 0; i < N; ++i) _broadcasts[i].WaitFinish();

            for (var i = 0; i < N; ++i)
            {
                Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminated");
                if (!(_broadcasts[i] is SilentProtocol<BinaryBroadcastId>))
                {
                    Assert.AreEqual(_resultInterceptors[i].ResultSet, 1, $"protocol {i} emitted result not once");
                    Assert.AreEqual(new BoolSet(true), _resultInterceptors[i].Result);
                }
            }
        }

        [Test]
        [Repeat(200)]
        public void TestRandomValues()
        {
            var random = new Random();
            SetupSomeSilent();

            var inputs = new int[N];
            int[] inputsCount = {0, 0};
            for (var i = 0; i < N; ++i)
            {
                inputs[i] = random.Next() % 2;
                if (_broadcasts[i] is SilentProtocol<BinaryBroadcastId>) continue;
                inputsCount[inputs[i]]++;
            }

            for (var i = 0; i < N; ++i)
                _broadcasters[i].InternalRequest(new ProtocolRequest<BinaryBroadcastId, bool>(
                    _resultInterceptors[i].Id, _broadcasts[i].Id as BinaryBroadcastId, inputs[i] == 1
                ));

            for (var i = 0; i < N; ++i) _broadcasts[i].WaitFinish();

            var received = new ISet<int>[N];
            for (var i = 0; i < N; ++i)
            {
                if (_broadcasts[i] is SilentProtocol<BinaryBroadcastId>) continue;
                received[i] = new SortedSet<int>();

                Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminated");
                var values = _resultInterceptors[i].Result;

                foreach (var b in values.Values())
                    received[i].Add(b ? 1 : 0);
            }

            ISet<int> firstReceived = null;
            for (var i = 0; i < N; ++i)
            {
                if (_broadcasts[i] is SilentProtocol<BinaryBroadcastId>) continue;
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
    }
}