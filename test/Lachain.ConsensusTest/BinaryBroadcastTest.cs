using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Lachain.Consensus;
using Lachain.Consensus.BinaryAgreement;
using Lachain.Consensus.Messages;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.ConsensusTest
{
    [TestFixture]
    public class BinaryBroadcastTest
    {
        private readonly Random _rnd = new Random();
        private DeliveryService _deliveryService = null!;
        private IConsensusProtocol[] _broadcasts = null!;
        private IConsensusBroadcaster[] _broadcasters = null!;
        private ProtocolInvoker<BinaryBroadcastId, BoolSet>[] _resultInterceptors = null!;
        private IPrivateConsensusKeySet[] _privateKeys = null!;
        private IPublicConsensusKeySet _publicKeys = null!;

        private void SetUp(int n, int f)
        {
            _deliveryService = new DeliveryService();
            _broadcasts = new IConsensusProtocol[n];
            _broadcasters = new IConsensusBroadcaster[n];
            _resultInterceptors = new ProtocolInvoker<BinaryBroadcastId, BoolSet>[n];
            _privateKeys = new IPrivateConsensusKeySet[n];
            _publicKeys = new PublicConsensusKeySet(n, f, null!, new Crypto.TPKE.PublicKey[]{},null!, Enumerable.Empty<ECDSAPublicKey>());
            for (var i = 0; i < n; ++i)
            {
                _resultInterceptors[i] = new ProtocolInvoker<BinaryBroadcastId, BoolSet>();
                _privateKeys[i] = TestUtils.EmptyWallet(n, f);
                _broadcasters[i] = new BroadcastSimulator(i, _publicKeys, _privateKeys[i], _deliveryService, false);
            }
        }

        private void SetUpAllHonest(int n, int f)
        {
            SetUp(n, f);
            for (uint i = 0; i < n; ++i)
            {
                _broadcasts[i] = new BinaryBroadcast(new BinaryBroadcastId(0, 0, 0), _publicKeys, _broadcasters[i]);
                _broadcasters[i].RegisterProtocols(new[] {_broadcasts[i], _resultInterceptors[i]});
            }
        }

        private void SetupSomeSilent(int n, int f)
        {
            SetUp(n, f);
            var cnt = 0;
            while (cnt < f)
            {
                var x = _rnd.Next(n);
                if (_broadcasts[x] != null) continue;
                _broadcasts[x] = new SilentProtocol<BinaryBroadcastId>(new BinaryBroadcastId(0, 0, 0));
                ++cnt;
            }

            for (uint i = 0; i < n; ++i)
            {
                _broadcasts[i] ??= new BinaryBroadcast(new BinaryBroadcastId(0, 0, 0), _publicKeys, _broadcasters[i]);
                _broadcasters[i].RegisterProtocols(new[] {_broadcasts[i], _resultInterceptors[i]});
            }
        }

        private void SetupOneSpammer(int n , int f, int spammer)
        {
            SetUp(n, f);
            for (uint i = 0; i < n; ++i)
            {
                if (i == spammer) continue;
                _broadcasts[i] = new BinaryBroadcast(new BinaryBroadcastId(0, 0, 0), _publicKeys, _broadcasters[i]);
            }
            _broadcasts[spammer] = new BinaryBroadcastSpammer(new BinaryBroadcastId(0, 0, 0), _publicKeys, _broadcasters[spammer]);
            for (var i = 0 ; i < n ; i++)
            {
                _broadcasters[i].RegisterProtocols(new[] {_broadcasts[i], _resultInterceptors[i]});
            }
        }

        [Test]
        public void TestBinaryBroadcastAllOne_7_2()
        {
            const int n = 7, f = 2;
            SetUpAllHonest(n, f);
            for (var i = 0; i < n; ++i)
                _broadcasters[i].InternalRequest(new ProtocolRequest<BinaryBroadcastId, bool>(
                    _resultInterceptors[i].Id, (_broadcasts[i].Id as BinaryBroadcastId)!, true
                ));

            for (var i = 0; i < n; ++i) _broadcasts[i].WaitFinish();

            for (var i = 0; i < n; ++i)
            {
                Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminated");
                Assert.AreEqual(_resultInterceptors[i].ResultSet, 1, $"protocol {i} emitted result not once");
                Assert.AreEqual(_resultInterceptors[i].ResultSet, _resultInterceptors[i].Result.Count);
                Assert.AreEqual(new BoolSet(true), _resultInterceptors[i].Result[0]);
            }
        }

        [Test]
        public void TestBinaryBroadcastAllZero_7_2()
        {
            const int n = 7, f = 2;
            SetUpAllHonest(n, f);
            for (var i = 0; i < n; ++i)
                _broadcasters[i].InternalRequest(new ProtocolRequest<BinaryBroadcastId, bool>(
                    _resultInterceptors[i].Id, (_broadcasts[i].Id as BinaryBroadcastId)!, false
                ));

            for (var i = 0; i < n; ++i) _broadcasts[i].WaitFinish();

            for (var i = 0; i < n; ++i)
            {
                Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminated");
                Assert.AreEqual(_resultInterceptors[i].ResultSet, 1, $"protocol {i} emitted result not once");
                Assert.AreEqual(_resultInterceptors[i].ResultSet, _resultInterceptors[i].Result.Count);
                Assert.AreEqual(new BoolSet(false), _resultInterceptors[i].Result[0]);
            }
        }

        [Test]
        public void TestRandomFailures_7_2()
        {
            const int n = 7, f = 2;
            SetupSomeSilent(n, f);

            for (var i = 0; i < n; ++i)
                _broadcasters[i].InternalRequest(new ProtocolRequest<BinaryBroadcastId, bool>(
                    _resultInterceptors[i].Id, (_broadcasts[i].Id as BinaryBroadcastId)!, true
                ));

            for (var i = 0; i < n; ++i) _broadcasts[i].WaitFinish();

            for (var i = 0; i < n; ++i)
            {
                Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminated");
                if (_broadcasts[i] is SilentProtocol<BinaryBroadcastId>) continue;
                Assert.AreEqual(_resultInterceptors[i].ResultSet, 1, $"protocol {i} emitted result not once");
                Assert.AreEqual(_resultInterceptors[i].ResultSet, _resultInterceptors[i].Result.Count);
                Assert.AreEqual(new BoolSet(true), _resultInterceptors[i].Result[0]);
            }
        }

        [Test]
        [Repeat(10)]
        public void TestRandomValues()
        {
            var n = _rnd.Next(4, 10);
            var f = _rnd.Next(1, (n - 1) / 3 + 1);
            SetupSomeSilent(n, f);

            var inputs = new int[n];
            int[] inputsCount = {0, 0};
            for (var i = 0; i < n; ++i)
            {
                inputs[i] = _rnd.Next(2);
                if (_broadcasts[i] is SilentProtocol<BinaryBroadcastId>) continue;
                inputsCount[inputs[i]]++;
            }

            for (var i = 0; i < n; ++i)
                _broadcasters[i].InternalRequest(new ProtocolRequest<BinaryBroadcastId, bool>(
                    _resultInterceptors[i].Id, (_broadcasts[i].Id as BinaryBroadcastId)!, inputs[i] == 1
                ));

            for (var i = 0; i < n; ++i) _broadcasts[i].WaitFinish();

            var received = new ISet<int>[n];
            for (var i = 0; i < n; ++i)
            {
                if (_broadcasts[i] is SilentProtocol<BinaryBroadcastId>) continue;
                received[i] = new SortedSet<int>();

                Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminated");
                Assert.AreEqual(1, _resultInterceptors[i].ResultSet);
                Assert.AreEqual(_resultInterceptors[i].ResultSet, _resultInterceptors[i].Result.Count);
                var values = _resultInterceptors[i].Result[0];

                foreach (var b in values.Values())
                    received[i].Add(b ? 1 : 0);
            }

            for (var i = 0; i < n; ++i)
            {
                if (_broadcasts[i] is SilentProtocol<BinaryBroadcastId>) continue;

                foreach(var v in received[i])
                {
                    Assert.IsTrue(v >= 0 && v < 2, "received value must be a binary.");
                    Assert.IsTrue(inputsCount[v] >= f + 1, "received value must be input of at least (f+1) correct processes.");
                }

                Assert.Greater(received[i].Count, 0, "all correct nodes should output something");
            }
        }

        [Test]
        public void TestOneSpammer_7_2()
        {
            const int n = 7, f = 2, spammerId = 6;
            SetupOneSpammer(n, f, spammerId);
            for (var i = 0; i < n; ++i)
                _broadcasters[i].InternalRequest(new ProtocolRequest<BinaryBroadcastId, bool>(
                    _resultInterceptors[i].Id, (_broadcasts[i].Id as BinaryBroadcastId)!, true
                ));
            // spammer will spam unwanted value 'false' N times to each validator
            (_broadcasts[spammerId] as BinaryBroadcastSpammer)!.SpamBVal(false);

            for (var i = 0; i < n; ++i) _broadcasts[i].WaitFinish();

            for (var i = 0; i < n; ++i)
            {
                Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminated");
                Assert.AreEqual(_resultInterceptors[i].ResultSet, 1, $"protocol {i} emitted result not once");
                Assert.AreEqual(_resultInterceptors[i].ResultSet, _resultInterceptors[i].Result.Count);
                Assert.AreEqual(new BoolSet(true), _resultInterceptors[i].Result[0]);
            }
        }
    }
}