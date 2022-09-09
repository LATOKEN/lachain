using System;
using System.Linq;
using NUnit.Framework;
using Lachain.Consensus;
using Lachain.Consensus.BinaryAgreement;
using Lachain.Consensus.Messages;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.ConsensusTest
{
    [TestFixture]
    public class BinaryAgreementTest
    {
        private readonly Random _rnd = new Random((int) TimeUtils.CurrentTimeMillis());
        private DeliveryService _deliveryService = null!;
        private IConsensusProtocol[] _broadcasts = null!;
        private IConsensusBroadcaster[] _broadcasters = null!;
        private ProtocolInvoker<BinaryAgreementId, bool>[] _resultInterceptors = null!;
        private IPrivateConsensusKeySet[] _privateKeys = null!;
        private IPublicConsensusKeySet _publicKeys = null!;

        private void SetUpAllHonest(int n, int f)
        {
            _deliveryService = new DeliveryService();
            _broadcasts = new IConsensusProtocol[n];
            _broadcasters = new IConsensusBroadcaster[n];
            _resultInterceptors = new ProtocolInvoker<BinaryAgreementId, bool>[n];
            _privateKeys = new IPrivateConsensusKeySet[n];
            var keygen = new TrustedKeyGen(n, f);
            var shares = keygen.GetPrivateShares().ToArray();
            var pubKeys = new PublicKeySet(shares.Select(share => share.GetPublicKeyShare()), f);

            _publicKeys = new PublicConsensusKeySet(n, f, null!, 
                new Crypto.TPKE.PublicKey[]{}, 
                pubKeys, Enumerable.Empty<ECDSAPublicKey>());
            for (var i = 0; i < n; ++i)
            {
                _resultInterceptors[i] = new ProtocolInvoker<BinaryAgreementId, bool>();
                _privateKeys[i] = new PrivateConsensusKeySet(null!, null!, shares[i]);
                _broadcasters[i] = new BroadcastSimulator(i, _publicKeys, _privateKeys[i], _deliveryService, true);
            }

            for (uint i = 0; i < n; ++i)
            {
                _broadcasts[i] = new BinaryAgreement(new BinaryAgreementId(0, 0), _publicKeys, _broadcasters[i]);
                _broadcasters[i].RegisterProtocols(new[] {_broadcasts[i], _resultInterceptors[i]});
            }
        }


        public void RunBinaryAgreementRandom(
            int n, int f, DeliveryServiceMode mode,
            int muteCnt = 0, double repeatProbability = .0
        )
        {
            SetUpAllHonest(n, f);
            _deliveryService.RepeatProbability = repeatProbability;
            _deliveryService.Mode = mode;
            while (_deliveryService.MutedPlayers.Count < muteCnt)
                _deliveryService.MutePlayer(_rnd.Next(0, n));

            var used = new BoolSet();
            for (var i = 0; i < n; ++i)
            {
                var cur = _rnd.Next() % 2 == 1;
                used = used.Add(cur);
                _broadcasters[i].InternalRequest(new ProtocolRequest<BinaryAgreementId, bool>(
                    _resultInterceptors[i].Id, (_broadcasts[i].Id as BinaryAgreementId)!, cur
                ));
            }

            for (var i = 0; i < n; ++i)
            {
                if (_deliveryService.MutedPlayers.Contains(i)) continue;
                _broadcasts[i].WaitResult();
            }

            _deliveryService.WaitFinish();

            for (var i = 0; i < n; ++i)
            {
                if (_deliveryService.MutedPlayers.Contains(i)) continue;

                Assert.AreEqual(
                    _resultInterceptors[i].ResultSet, 1,
                    $"protocol has {i} emitted result not once but {_resultInterceptors[i].ResultSet}"
                );
                Assert.AreEqual(_resultInterceptors[i].ResultSet, _resultInterceptors[i].Result.Count);
            }

            bool? res = null;
            for (var i = 0; i < n; ++i)
            {
                if (_deliveryService.MutedPlayers.Contains(i)) continue;
                var ans = _resultInterceptors[i].Result[0];
                res ??= ans;
                Assert.AreEqual(res, _resultInterceptors[i].Result[0]);
            }

            Assert.IsNotNull(res);
            Assert.True(used.Contains(res!.Value));

            for (var i = 0; i < n; ++i)
            {
                _broadcasts[i].Terminate();
                _broadcasts[i].WaitFinish();
            }
        }

        [Test]
        [Repeat(3)]
        public void RandomTestLast_10_3()
        {
            RunBinaryAgreementRandom(10, 3, DeliveryServiceMode.TAKE_LAST);
        }

        [Test]
        [Repeat(3)]
        public void RandomTestLast_4_1()
        {
            RunBinaryAgreementRandom(4, 1, DeliveryServiceMode.TAKE_LAST);
        }

        [Test]
        [Repeat(3)]
        public void RandomTestLast_7_2()
        {
            RunBinaryAgreementRandom(7, 2, DeliveryServiceMode.TAKE_LAST);
        }

        [Test]
        [Repeat(3)]
        public void RandomTestLastWithMuted_10_3()
        {
            RunBinaryAgreementRandom(10, 3, DeliveryServiceMode.TAKE_LAST, 3);
        }

        [Test]
        [Repeat(3)]
        public void RandomTestLastWithMuted_4_1()
        {
            RunBinaryAgreementRandom(4, 1, DeliveryServiceMode.TAKE_LAST, 1);
        }

        [Test]
        [Repeat(3)]
        public void RandomTestLastWithMuted_7_2()
        {
            RunBinaryAgreementRandom(7, 2, DeliveryServiceMode.TAKE_LAST, 2);
        }

        [Test]
        [Repeat(3)]
        public void RandomTestRandom_4_1()
        {
            RunBinaryAgreementRandom(4, 1, DeliveryServiceMode.TAKE_RANDOM);
        }

        [Test]
        [Repeat(10)]
        public void FullyRandomTest()
        {
            var n = _rnd.Next(1, 10);
            var f = _rnd.Next((n - 1) / 3 + 1);
            var mode = _rnd.SelectRandom(Enum.GetValues(typeof(DeliveryServiceMode)).Cast<DeliveryServiceMode>());
            RunBinaryAgreementRandom(n, f, mode);
        }

        [Test]
        [Repeat(3)]
        public void RandomTestRandomWithMuted_4_1()
        {
            RunBinaryAgreementRandom(4, 1, DeliveryServiceMode.TAKE_RANDOM, 1);
        }

        [Test]
        [Repeat(3)]
        public void RandomTestRandomWithRepeat_4_1()
        {
            RunBinaryAgreementRandom(4, 1, DeliveryServiceMode.TAKE_RANDOM, 0, .2);
        }

        [Test]
        public void TestBinaryAgreementAllOnes_7_2()
        {
            const int n = 7, f = 2;
            SetUpAllHonest(n, f);
            for (var i = 0; i < n; ++i)
                _broadcasters[i].InternalRequest(new ProtocolRequest<BinaryAgreementId, bool>(
                    _resultInterceptors[i].Id, (_broadcasts[i].Id as BinaryAgreementId)!, true
                ));

            for (var i = 0; i < n; ++i) _broadcasts[i].WaitResult();

            for (var i = 0; i < n; ++i)
            {
                _broadcasts[i].Terminate();
                _broadcasts[i].WaitFinish();
            }

            _deliveryService.WaitFinish();

            for (var i = 0; i < n; ++i)
            {
                Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminated");
                Assert.AreEqual(_resultInterceptors[i].ResultSet, 1,
                    $"protocol has {i} emitted result not once but {_resultInterceptors[i].ResultSet}");
                Assert.AreEqual(_resultInterceptors[i].ResultSet, _resultInterceptors[i].Result.Count);
                Assert.AreEqual(true, _resultInterceptors[i].Result[0]);
            }
        }

        [Test]
        public void TestBinaryAgreementAllZero_7_2()
        {
            const int n = 7, f = 2;
            SetUpAllHonest(n, f);
            for (var i = 0; i < n; ++i)
                _broadcasters[i].InternalRequest(new ProtocolRequest<BinaryAgreementId, bool>(
                    _resultInterceptors[i].Id, (_broadcasts[i].Id as BinaryAgreementId)!, false
                ));

            for (var i = 0; i < n; ++i) _broadcasts[i].WaitResult();

            _deliveryService.WaitFinish();

            for (var i = 0; i < n; ++i)
            {
                _broadcasts[i].Terminate();
                _broadcasts[i].WaitFinish();
            }

            for (var i = 0; i < n; ++i)
            {
                Assert.AreEqual(_resultInterceptors[i].ResultSet, 1,
                    $"protocol has {i} emitted result not once but {_resultInterceptors[i].ResultSet}");
                Assert.AreEqual(_resultInterceptors[i].ResultSet, _resultInterceptors[i].Result.Count);
                Assert.AreEqual(false, _resultInterceptors[i].Result[0]);
            }
        }
    }
}