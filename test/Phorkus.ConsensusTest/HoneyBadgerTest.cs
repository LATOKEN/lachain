using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Phorkus.Consensus;
using Phorkus.Consensus.HoneyBadger;
using Phorkus.Consensus.Messages;
using Phorkus.Consensus.TPKE;
using Phorkus.Crypto;
using Phorkus.Crypto.MCL.BLS12_381;
using Phorkus.Crypto.ThresholdSignature;

namespace Phorkus.ConsensusTest
{
    [TestFixture]
    public class HoneyBadgerTest
    {
        [SetUp]
        public void SetUp()
        {
            _rnd = new Random();
            Mcl.Init();
            _deliveryService = new DeliveryService();
            _broadcasts = new IConsensusProtocol[N];
            _broadcasters = new IConsensusBroadcaster[N];
            _resultInterceptors = new ProtocolInvoker<HoneyBadgerId, ISet<IRawShare>>[N];
            var keygen = new TrustedKeyGen(N, F);
            var shares = keygen.GetPrivateShares().ToArray();
            var pubKeys = new PublicKeySet(shares.Select(share => share.GetPublicKeyShare()), F);
            var tpkeKeygen = new Crypto.TPKE.TrustedKeyGen(N, F);

            var ecdsaKeys = Enumerable.Range(0, N)
                .Select(i => _crypto.GenerateRandomBytes(32))
                .Select(x => x.ToPrivateKey())
                .Select(k => new ECDSAKeyPair(k, _crypto))
                .ToArray();
            _publicKeys = new PublicConsensusKeySet(
                N, F, tpkeKeygen.GetPubKey(), tpkeKeygen.GetVerificationKey(),
                pubKeys, ecdsaKeys.Select(k => k.PublicKey)
            );
            _privateKeys = new IPrivateConsensusKeySet[N];
            for (var i = 0; i < N; ++i)
            {
                _resultInterceptors[i] = new ProtocolInvoker<HoneyBadgerId, ISet<IRawShare>>();
                _privateKeys[i] = new PrivateConsensusKeySet(ecdsaKeys[i], tpkeKeygen.GetPrivKey(i), shares[i]);
                _broadcasters[i] = new BroadcastSimulator(i, _publicKeys, _privateKeys[i], _deliveryService, true);
            }
        }

        private DeliveryService _deliveryService;
        private IConsensusProtocol[] _broadcasts;
        private IConsensusBroadcaster[] _broadcasters;
        private ProtocolInvoker<HoneyBadgerId, ISet<IRawShare>>[] _resultInterceptors;
        private const int N = 7;
        private const int F = 2;
        private IPublicConsensusKeySet _publicKeys;
        private IPrivateConsensusKeySet[] _privateKeys;
        private Random _rnd;
        private readonly ICrypto _crypto = CryptoProvider.GetCrypto();

        private void SetUpAllHonest()
        {
            for (uint i = 0; i < N; ++i)
            {
                _broadcasts[i] = new HoneyBadger(
                    new HoneyBadgerId(10), _publicKeys, _privateKeys[i].TpkePrivateKey, _broadcasters[i]
                );
                _broadcasters[i].RegisterProtocols(new[] {_broadcasts[i], _resultInterceptors[i]});
            }
        }

        private void SetUpSomeSilent(ISet<int> s)
        {
            for (var i = 0; i < N; ++i)
            {
                _broadcasts[i] = new HoneyBadger(
                    new HoneyBadgerId(10), _publicKeys, _privateKeys[i].TpkePrivateKey, _broadcasters[i]
                );
                _broadcasters[i].RegisterProtocols(new[] {_broadcasts[i], _resultInterceptors[i]});
                foreach (var j in s) (_broadcasters[i] as BroadcastSimulator)?.Silent(j);
            }
        }

        [Test]
        public void TestAllHonest()
        {
            SetUpAllHonest();
            for (var i = 0; i < N; ++i)
            {
                var share = new RawShare(new byte[32], i);
                _broadcasters[i].InternalRequest(new ProtocolRequest<HoneyBadgerId, IRawShare>(
                    _resultInterceptors[i].Id, _broadcasts[i].Id as HoneyBadgerId, share
                ));
            }

            for (var i = 0; i < N; ++i) _broadcasts[i].WaitFinish();

            for (var i = 0; i < N; ++i)
            {
                Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminated");
                Assert.AreEqual(_resultInterceptors[i].ResultSet, 1,
                    $"protocol {i} has emitted result not once but {_resultInterceptors[i].ResultSet}");
                Assert.AreEqual(N, _resultInterceptors[i].Result.Count);
            }
        }

        [Test]
        public void TestSomeSilent()
        {
            var s = new HashSet<int>();
            while (s.Count < F) s.Add(_rnd.Next(0, N - 1));

            SetUpSomeSilent(s);
            for (var i = 0; i < N; ++i)
            {
                var share = new RawShare(new byte[32], i);
                _broadcasters[i].InternalRequest(new ProtocolRequest<HoneyBadgerId, IRawShare>(
                    _resultInterceptors[i].Id, _broadcasts[i].Id as HoneyBadgerId, share
                ));
            }

            for (var i = 0; i < N; ++i)
            {
                if (s.Contains(i)) continue;
                _broadcasts[i].WaitFinish();
            }

            for (var i = 0; i < N; ++i)
            {
                if (s.Contains(i)) continue;

                Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminated");
                Assert.AreEqual(_resultInterceptors[i].ResultSet, 1,
                    $"protocol {i} has emitted result not once but {_resultInterceptors[i].ResultSet}");
                Assert.AreEqual(N - F, _resultInterceptors[i].Result.Count);
            }
        }
    }
}