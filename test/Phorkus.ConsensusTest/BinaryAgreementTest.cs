using System;
using System.Linq;
using NUnit.Framework;
using Phorkus.Consensus;
using Phorkus.Consensus.BinaryAgreement;
using Phorkus.Consensus.CommonCoin.ThresholdSignature;
using Phorkus.Consensus.Messages;
using Phorkus.Crypto.MCL.BLS12_381;
using Phorkus.Utility.Utils;

namespace Phorkus.ConsensusTest
{
    [TestFixture]
    public class BinaryAgreementTest 
    {
        private DeliverySerivce _deliverySerivce;
        private IConsensusProtocol[] _broadcasts;
        private IConsensusBroadcaster[] _broadcasters;
        private ProtocolInvoker<BinaryAgreementId, bool>[] _resultInterceptors;
        private const int N = 7;
        private const int F = 2;
        private IWallet[] _wallets;
        private Random _rnd;

        [SetUp]
        public void SetUp()
        {
            _rnd = new Random();
            Mcl.Init();
            _deliverySerivce = new DeliverySerivce();
            _broadcasts = new IConsensusProtocol[N];
            _broadcasters = new IConsensusBroadcaster[N];
            _resultInterceptors = new ProtocolInvoker<BinaryAgreementId, bool>[N];
            _wallets = new IWallet[N];
            var keygen = new TrustedKeyGen(N, F, new Random(0x0badfee0));
            var shares = keygen.GetPrivateShares().ToArray();
            var pubKeys = new PublicKeySet(shares.Select(share => share.GetPublicKeyShare()), F);
            for (var i = 0; i < N; ++i)
            {
                _resultInterceptors[i] = new ProtocolInvoker<BinaryAgreementId, bool>();
                _wallets[i] = new Wallet(N, F) {PrivateKeyShare = shares[i], PublicKeySet = pubKeys};
                _broadcasters[i] = new BroadcastSimulator(i, _wallets[i], _deliverySerivce,true);
            }
        }

        private void SetUpAllHonest()
        {
            for (uint i = 0; i < N; ++i)
            {
                _broadcasts[i] = new BinaryAgreement(new BinaryAgreementId(0, 0), _wallets[i], _broadcasters[i]);
                _broadcasters[i].RegisterProtocols(new[] {_broadcasts[i], _resultInterceptors[i]});
            }
        }

        [Test]
        public void TestBinaryAgreementAllZero()
        {
            SetUpAllHonest();
            for (var i = 0; i < N; ++i)
            {
                _broadcasters[i].InternalRequest(new ProtocolRequest<BinaryAgreementId, bool>(
                    _resultInterceptors[i].Id, _broadcasts[i].Id as BinaryAgreementId, false
                ));
            }

            for (var i = 0; i < N; ++i)
            {
                _broadcasts[i].WaitFinish();
            }
            _deliverySerivce.WaitFinish();

            for (var i = 0; i < N; ++i)
            {
                Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminated");
                Assert.AreEqual(_resultInterceptors[i].ResultSet, 1, $"protocol has {i} emitted result not once but {_resultInterceptors[i].ResultSet}");
                Assert.AreEqual(false, _resultInterceptors[i].Result);
            }
        }
        
        [Test]
        public void TestBinaryAgreementAllOnes()
        {
            SetUpAllHonest();
            for (var i = 0; i < N; ++i)
            {
                _broadcasters[i].InternalRequest(new ProtocolRequest<BinaryAgreementId, bool>(
                    _resultInterceptors[i].Id, _broadcasts[i].Id as BinaryAgreementId, true
                ));
            }

            for (var i = 0; i < N; ++i)
            {
                _broadcasts[i].WaitFinish();
            }
            _deliverySerivce.WaitFinish();

            for (var i = 0; i < N; ++i)
            {
                Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminated");
                Assert.AreEqual(_resultInterceptors[i].ResultSet, 1, $"protocol has {i} emitted result not once but {_resultInterceptors[i].ResultSet}");
                Assert.AreEqual(true, _resultInterceptors[i].Result);
            }
        }
        
        [Test]
        [Repeat(100)]
        public void TestBinaryAgreementRandom()
        {
            SetUpAllHonest();
            var used = new BoolSet();
            for (var i = 0; i < N; ++i)
            {
                var cur = _rnd.Next() % 2 == 1;
                used = used.Add(cur);
                _broadcasters[i].InternalRequest(new ProtocolRequest<BinaryAgreementId, bool>(
                    _resultInterceptors[i].Id, _broadcasts[i].Id as BinaryAgreementId, cur 
                ));
            }

            for (var i = 0; i < N; ++i)
            {
                _broadcasts[i].WaitFinish();
            }
            _deliverySerivce.WaitFinish();
            
            for (var i = 0; i < N; ++i)
            {
                Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminated");
                Assert.AreEqual(_resultInterceptors[i].ResultSet, 1, $"protocol has {i} emitted result not once but {_resultInterceptors[i].ResultSet}");
            }

            var res = _resultInterceptors[0].Result;
            for (var i = 0; i < N; ++i)
            {
                var ans = _resultInterceptors[i].Result;
                Assert.AreEqual(res, _resultInterceptors[i].Result);
            }
            Assert.True(used.Contains(res));
        }

    }
}