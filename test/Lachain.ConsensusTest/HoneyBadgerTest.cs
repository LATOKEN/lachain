using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Lachain.Consensus;
using Lachain.Consensus.HoneyBadger;
using Lachain.Consensus.Messages;
using Lachain.Crypto;
using Lachain.Crypto.ECDSA;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Utility.Utils;

namespace Lachain.ConsensusTest
{
    [TestFixture]
    public class HoneyBadgerTest
    {
        private const int Era = 0;

        private readonly Random _rnd = new Random();
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();

        private DeliveryService _deliveryService = null!;
        private IConsensusProtocol[] _broadcasts = null!;
        private IConsensusBroadcaster[] _broadcasters = null!;
        private ProtocolInvoker<HoneyBadgerId, ISet<IRawShare>>[] _resultInterceptors = null!;
        private IPublicConsensusKeySet _publicKeys = null!;
        private IPrivateConsensusKeySet[] _privateKeys = null!;

        public void SetUp(int n, int f)
        {
            _deliveryService = new DeliveryService();
            _broadcasts = new IConsensusProtocol[n];
            _broadcasters = new IConsensusBroadcaster[n];
            _resultInterceptors = new ProtocolInvoker<HoneyBadgerId, ISet<IRawShare>>[n];
            var keygen = new TrustedKeyGen(n, f);
            var shares = keygen.GetPrivateShares().ToArray();
            var pubKeys = new PublicKeySet(shares.Select(share => share.GetPublicKeyShare()), f);
            var tpkeKeygen = new Crypto.TPKE.TrustedKeyGen(n, f);

            var ecdsaKeys = Enumerable.Range(0, n)
                .Select(i => Crypto.GenerateRandomBytes(32))
                .Select(x => x.ToPrivateKey())
                .Select(k => new EcdsaKeyPair(k))
                .ToArray();
            _publicKeys = new PublicConsensusKeySet(n, f, tpkeKeygen.GetPubKey(), pubKeys,
                ecdsaKeys.Select(k => k.PublicKey));
            _privateKeys = new IPrivateConsensusKeySet[n];
            for (var i = 0; i < n; ++i)
            {
                _resultInterceptors[i] = new ProtocolInvoker<HoneyBadgerId, ISet<IRawShare>>();
                _privateKeys[i] = new PrivateConsensusKeySet(ecdsaKeys[i], tpkeKeygen.GetPrivKey(i), shares[i]);
                _broadcasters[i] = new BroadcastSimulator(i, _publicKeys, _privateKeys[i], _deliveryService, true);
            }
        }

        private void SetUpAllHonest(int n, int f)
        {
            SetUp(n, f);
            for (uint i = 0; i < n; ++i)
            {
                _broadcasts[i] = new HoneyBadger(
                    new HoneyBadgerId(Era), _publicKeys, _privateKeys[i].TpkePrivateKey, _broadcasters[i]
                );
                _broadcasters[i].RegisterProtocols(new[] {_broadcasts[i], _resultInterceptors[i]});
            }
        }

        private void SetUpOneMalicious(int n, int f)
        {
            SetUp(n, f);
            _broadcasts[0] = new HoneyBadgerMalicious(
                new HoneyBadgerId(Era), _publicKeys, _privateKeys[0].TpkePrivateKey, _broadcasters[0]
            );
            _broadcasters[0].RegisterProtocols(new[] {_broadcasts[0], _resultInterceptors[0]});
            for (uint i = 1; i < n; ++i)
            {
                _broadcasts[i] = new HoneyBadger(
                    new HoneyBadgerId(Era), _publicKeys, _privateKeys[i].TpkePrivateKey, _broadcasters[i]
                );
                _broadcasters[i].RegisterProtocols(new[] {_broadcasts[i], _resultInterceptors[i]});
            }
        }

        private void SetUpSomeSilent(int n, int f, ISet<int> s)
        {
            SetUp(n, f);
            for (var i = 0; i < n; ++i)
            {
                _broadcasts[i] = new HoneyBadger(
                    new HoneyBadgerId(Era), _publicKeys, _privateKeys[i].TpkePrivateKey, _broadcasters[i]
                );
                _broadcasters[i].RegisterProtocols(new[] {_broadcasts[i], _resultInterceptors[i]});
                foreach (var j in s) (_broadcasters[i] as BroadcastSimulator)?.Silent(j);
            }
        }

        [Test]
        public void TestAllHonest_7_2()
        {
            const int n = 7, f = 2;
            SetUpAllHonest(n, f);
            for (var i = 0; i < n; ++i)
            {
                var share = new RawShare(new byte[32], i);
                _broadcasters[i].InternalRequest(new ProtocolRequest<HoneyBadgerId, IRawShare>(
                    _resultInterceptors[i].Id, (_broadcasts[i].Id as HoneyBadgerId)!, share
                ));
            }

            for (var i = 0; i < n; ++i) _broadcasts[i].WaitFinish();
            for (var i = 0; i < n; ++i)
            {
                Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminated");
                Assert.AreEqual(_resultInterceptors[i].ResultSet, 1,
                    $"protocol {i} has emitted result not once but {_resultInterceptors[i].ResultSet}");
                Assert.AreEqual(n, _resultInterceptors[i].Result.Count);
            }
        }

        [Test]
        public void TestSomeSilent_7_2()
        {
            const int n = 7, f = 2;
            var s = new HashSet<int>();
            while (s.Count < f) s.Add(_rnd.Next(n));

            SetUpSomeSilent(n, f, s);
            for (var i = 0; i < n; ++i)
            {
                var share = new RawShare(new byte[32], i);
                _broadcasters[i].InternalRequest(new ProtocolRequest<HoneyBadgerId, IRawShare>(
                    _resultInterceptors[i].Id, (_broadcasts[i].Id as HoneyBadgerId)!, share
                ));
            }

            for (var i = 0; i < n; ++i)
            {
                if (s.Contains(i)) continue;
                _broadcasts[i].WaitFinish();
            }

            for (var i = 0; i < n; ++i)
            {
                if (s.Contains(i)) continue;

                Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminated");
                Assert.AreEqual(_resultInterceptors[i].ResultSet, 1,
                    $"protocol {i} has emitted result not once but {_resultInterceptors[i].ResultSet}");
                Assert.AreEqual(n - f, _resultInterceptors[i].Result.Count);
            }
        }

        [Test]
        [Ignore("test")]
        public void TestSomeMalicious_7_2()
        {
            const int n = 7, f = 2;
 
            SetUpOneMalicious(n, f);
            for (var i = 0; i < n; ++i)
            {
                var share = new RawShare(new byte[32], i);
                _broadcasters[i].InternalRequest(new ProtocolRequest<HoneyBadgerId, IRawShare>(
                    _resultInterceptors[i].Id, (_broadcasts[i].Id as HoneyBadgerId)!, share
                ));
            }

            for (var i = 1; i < n; ++i)
            {
                _broadcasts[i].WaitFinish();
            }

            for (var i = 1; i < n; ++i)
            {
                Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminated");
                Assert.AreEqual(_resultInterceptors[i].ResultSet, 1,
                    $"protocol {i} has emitted result not once but {_resultInterceptors[i].ResultSet}");
                Assert.AreEqual(n - f, _resultInterceptors[i].Result.Count);
            }
        }

        [Test]
        [Repeat(5)]
        public void RandomTest()
        {
            var n = _rnd.Next(1, 10);
            var f = _rnd.Next((n - 1) / 3 + 1);
            var mode = _rnd.SelectRandom(Enum.GetValues(typeof(DeliveryServiceMode)).Cast<DeliveryServiceMode>());
            var s = new HashSet<int>();
            while (s.Count < f) s.Add(_rnd.Next(n));

            SetUpSomeSilent(n, f, s);
            _deliveryService.Mode = mode;
            for (var i = 0; i < n; ++i)
            {
                var share = new RawShare(new byte[32], i);
                _broadcasters[i].InternalRequest(new ProtocolRequest<HoneyBadgerId, IRawShare>(
                    _resultInterceptors[i].Id, (_broadcasts[i].Id as HoneyBadgerId)!, share
                ));
            }

            for (var i = 0; i < n; ++i)
            {
                if (s.Contains(i)) continue;
                _broadcasts[i].WaitFinish();
            }

            for (var i = 0; i < n; ++i)
            {
                if (s.Contains(i)) continue;

                Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminated");
                Assert.AreEqual(_resultInterceptors[i].ResultSet, 1,
                    $"protocol {i} has emitted result not once but {_resultInterceptors[i].ResultSet}");
                Assert.AreEqual(n - f, _resultInterceptors[i].Result.Count);
            }
        }
    }
}