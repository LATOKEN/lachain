using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Lachain.Consensus;
using Lachain.Consensus.Messages;
using Lachain.Consensus.TPKE;
using Lachain.Crypto;
using Lachain.Crypto.MCL.BLS12_381;
using Lachain.Crypto.TPKE;
using Lachain.Proto;

namespace Lachain.ConsensusTest
{
    [TestFixture]
    public class TPKEDealerSetupTest
    {
        [SetUp]
        public void SetUp()
        {
            _rnd = new Random();
            _deliveryService = new DeliveryService();
            _broadcasts = new IConsensusProtocol[N];
            _broadcasters = new IConsensusBroadcaster[N];
            _resultInterceptors = new ProtocolInvoker<TPKESetupId, Keys>[N];
            _wallets = new IPrivateConsensusKeySet[N];
            _publicKeys = new PublicConsensusKeySet(
                N, T, null, null,
                null, Enumerable.Empty<ECDSAPublicKey>()
            );
            for (var i = 0; i < N; ++i)
            {
                _resultInterceptors[i] = new ProtocolInvoker<TPKESetupId, Keys>();
                _wallets[i] = TestUtils.EmptyWallet(N, T);
                _broadcasters[i] = new BroadcastSimulator(i, _publicKeys, _wallets[i], _deliveryService, false);
            }

            Mcl.Init();
        }

        private DeliveryService _deliveryService;
        private IConsensusProtocol[] _broadcasts;
        private IConsensusBroadcaster[] _broadcasters;
        private ProtocolInvoker<TPKESetupId, Keys>[] _resultInterceptors;
        private const int N = 10;
        private const int T = 5;
        private Random _rnd;
        private IPrivateConsensusKeySet[] _wallets;
        private IPublicConsensusKeySet _publicKeys;

        private void SetUpAllHonest()
        {
            for (uint i = 0; i < N; ++i)
            {
                _broadcasts[i] = new TPKEDealerSetup(new TPKESetupId(0), _publicKeys, _broadcasters[i]);
                _broadcasters[i].RegisterProtocols(new[] {_broadcasts[i], _resultInterceptors[i]});
            }
        }

        private void RunAllHonest()
        {
            SetUpAllHonest();
            for (var i = 0; i < N; ++i)
                _broadcasters[i].InternalRequest(new ProtocolRequest<TPKESetupId, object>(
                    _resultInterceptors[i].Id, _broadcasts[i].Id as TPKESetupId, null
                ));

            for (var i = 0; i < N; ++i) _broadcasts[i].WaitFinish();

            for (var i = 0; i < N; ++i) Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminated");
        }

        private ISet<int> ChooseRandomPlayers(int t)
        {
            ISet<int> S = new HashSet<int>();
            while (S.Count < t) S.Add(_rnd.Next(0, N - 1));

            return S;
        }

        [Test]
        [Repeat(100)]
        public void TestEncryptionDecryption()
        {
            var data = new byte[32];
            _rnd.NextBytes(data);

            RunAllHonest();
            var pubKey = _resultInterceptors[0].Result.PubKey;

            var share = new RawShare(data, 555);
            var enc = pubKey.Encrypt(share);

            var parts = new List<PartiallyDecryptedShare>();
            foreach (var i in ChooseRandomPlayers(T)) parts.Add(_resultInterceptors[i].Result.PrivKey.Decrypt(enc));

            var dec = pubKey.FullDecrypt(enc, parts);

            Assert.True(share.Equals(dec));
        }

        [Test]
        [Repeat(100)]
        public void TestLagrangeInterpolation()
        {
            RunAllHonest();

            // test that pub key can be recovered correctly using interpolation

            var ys = new List<G1>();
            var xs = new List<Fr>();

            foreach (var i in ChooseRandomPlayers(T))
            {
                xs.Add(Fr.FromInt(i + 1));
                ys.Add(_resultInterceptors[i].Result.PrivKey.Y);
            }

            var A = Mcl.LagrangeInterpolateG1(xs.ToArray(), ys.ToArray());
            var B = _resultInterceptors[0].Result.PubKey.Y;
//            Console.Error.WriteLine(B.GetStr(0));
            Assert.True(B.Equals(A), "interpolated pubkey equals to real pubkey");
        }

        [Test]
        public void TestVerification()
        {
            var data = new byte[32];
            _rnd.NextBytes(data);

            RunAllHonest();
            var pubKey = _resultInterceptors[0].Result.PubKey;

            var share = new RawShare(data, 555);
            var enc = pubKey.Encrypt(share);

            for (var j = 0; j < N; ++j)
            {
                var vk = _resultInterceptors[j].Result.VerificationKey;
                for (var i = 0; i < N; ++i)
                {
                    var part = _resultInterceptors[i].Result.PrivKey.Decrypt(enc);
                    Assert.True(vk.Verify(enc, part));
                }
            }
        }
    }
}