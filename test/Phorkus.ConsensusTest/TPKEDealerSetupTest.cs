using System;
using System.Collections.Generic;
using NUnit.Framework;
using Phorkus.Consensus;
using Phorkus.Consensus.Messages;
using Phorkus.Consensus.TPKE;
using Phorkus.Crypto.MCL.BLS12_381;

namespace Phorkus.ConsensusTest
{
    [TestFixture]
    public class TPKEDealerSetupTest
    {
        private PlayerSet _playerSet;
        private IConsensusProtocol[] _broadcasts;
        private IConsensusBroadcaster[] _broadcasters;
        private ProtocolInvoker<TPKESetupId, TPKEKeys>[] _resultInterceptors;
        private const int N = 10;
        private const int T = 5;
        private Random _rnd;
        private IWallet[] _wallets;

        [SetUp]
        public void SetUp()
        {
            _rnd = new Random();
            _playerSet = new PlayerSet();
            _broadcasts = new IConsensusProtocol[N];
            _broadcasters = new IConsensusBroadcaster[N];
            _resultInterceptors = new ProtocolInvoker<TPKESetupId, TPKEKeys>[N];
            _wallets = new IWallet[N];
            for (var i = 0; i < N; ++i)
            {
                _resultInterceptors[i] = new ProtocolInvoker<TPKESetupId, TPKEKeys>();
                _wallets[i] = new Wallet(N, T);
                _broadcasters[i] = new BroadcastSimulator(i, _wallets[i], _playerSet);
            }
            
            Mcl.Init();
        }

        private void SetUpAllHonest()
        {
            for (uint i = 0; i < N; ++i)
            {
                _broadcasts[i] = new TPKEDealerSetup(new TPKESetupId(0), _wallets[i], _broadcasters[i]);
                _broadcasters[i].RegisterProtocols(new[] {_broadcasts[i], _resultInterceptors[i]});
            }
        }

        private void RunAllHonest()
        {
            SetUpAllHonest();
            for (var i = 0; i < N; ++i)
            {
                _broadcasters[i].InternalRequest(new ProtocolRequest<TPKESetupId, object>(
                    _resultInterceptors[i].Id, _broadcasts[i].Id as TPKESetupId, null
                ));
            }

            for (var i = 0; i < N; ++i)
            {
                _broadcasts[i].WaitFinish();
            }

            for (var i = 0; i < N; ++i)
            {
                Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminated");
            }
        }

        private ISet<int> ChooseRandomPlayers(int t)
        {
            ISet<int> S = new HashSet<int>();
            while (S.Count < t)
            {
                S.Add(_rnd.Next(0, N - 1));
            }

            return S;
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
            foreach (var i in ChooseRandomPlayers(T))
            {
                parts.Add(_resultInterceptors[i].Result.PrivKey.Decrypt(enc));
            }

            var dec = pubKey.FullDecrypt(enc, parts);
            
            Assert.True(share.Equals(dec));
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

            for (int j = 0; j < N; ++j)
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