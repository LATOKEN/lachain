using System;
using System.Linq;
using NUnit.Framework;
using Phorkus.Consensus;
using Phorkus.Consensus.BinaryAgreement;
using Phorkus.Consensus.Messages;
using Phorkus.Crypto.MCL.BLS12_381;
using Phorkus.Crypto.ThresholdSignature;
using Phorkus.Utility.Utils;

namespace Phorkus.ConsensusTest
{
    [TestFixture]
    public class BinaryAgreementTest 
    {
        private DeliveryService _deliveryService;
        private IConsensusProtocol[] _broadcasts;
        private IConsensusBroadcaster[] _broadcasters;
        private ProtocolInvoker<BinaryAgreementId, bool>[] _resultInterceptors;
        private int F = 1;
        private int N = 3 * 1 + 1;
        private IWallet[] _wallets;
        private Random _rnd;

//        [SetUp]
        public void SetUp()
        {
            _rnd = new Random();
            Mcl.Init();
            _deliveryService = new DeliveryService();
            _broadcasts = new IConsensusProtocol[N];
            _broadcasters = new IConsensusBroadcaster[N];
            _resultInterceptors = new ProtocolInvoker<BinaryAgreementId, bool>[N];
            _wallets = new IWallet[N];
            var keygen = new TrustedKeyGen(N, F);
            var shares = keygen.GetPrivateShares().ToArray();
            var pubKeys = new PublicKeySet(shares.Select(share => share.GetPublicKeyShare()), F);
            for (var i = 0; i < N; ++i)
            {
                _resultInterceptors[i] = new ProtocolInvoker<BinaryAgreementId, bool>();
                _wallets[i] = new Wallet(N, F) {ThresholdSignaturePrivateKeyShare = shares[i], ThresholdSignaturePublicKeySet = pubKeys};
                _broadcasters[i] = new BroadcastSimulator(i, _wallets[i], _deliveryService,true);
            }
        }

        private void SetUpAllHonest()
        {
            SetUp();
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
                _broadcasts[i].WaitResult();
            }
            Console.Error.WriteLine("-------------------------------------------------------------------------- ZHOPA ####################################################33");
            _deliveryService.WaitFinish();
            Console.Error.WriteLine("-------------------------------------------------------------------------- ZHOPA ####################################################33");
            
            for (var i = 0; i < N; ++i)
            {
                _broadcasts[i].Terminate();
                Console.Error.WriteLine($"boy {i} terminated");
                _broadcasts[i].WaitFinish();
                Console.Error.WriteLine($"boy {i} finished");
            }
            Console.Error.WriteLine("-------------------------------------------------------------------------- ZHOPA ####################################################33");

            for (var i = 0; i < N; ++i)
            {
//                Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminated");
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
                _broadcasts[i].WaitResult();
            }
            
            for (var i = 0; i < N; ++i)
            {
                _broadcasts[i].Terminate();
                _broadcasts[i].WaitFinish();
            }
            _deliveryService.WaitFinish();

            for (var i = 0; i < N; ++i)
            {
                Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminated");
                Assert.AreEqual(_resultInterceptors[i].ResultSet, 1, $"protocol has {i} emitted result not once but {_resultInterceptors[i].ResultSet}");
                Assert.AreEqual(true, _resultInterceptors[i].Result);
            }
        }
        
        
        public void RunBinaryAgreementRandom(int n, int f, DeliveryServiceMode mode, int muteCnt = 0, double repeatProbability = .0)
        {
            N = n;
            F = f;
            SetUpAllHonest();
            _deliveryService.RepeatProbability = repeatProbability;
            _deliveryService.Mode = mode;
            while (_deliveryService._mutedPlayers.Count < muteCnt)
            {
                _deliveryService.MutePlayer(_rnd.Next(0, N - 1));
            }

            Console.Error.WriteLine("------------------------------------------------------------------- NEW ITERATION ------------------------------------------------------------------------------------------------------------------------------------------------------");
            
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
                if (_deliveryService._mutedPlayers.Contains(i)) continue;
                _broadcasts[i].WaitResult();
            }
            
            Console.Error.WriteLine("All players produced result");
            _deliveryService.WaitFinish();
            Console.Error.WriteLine("Delivery service shut down");
            
            for (var i = 0; i < N; ++i)
            {
                if (_deliveryService._mutedPlayers.Contains(i)) continue;
                
                Assert.AreEqual(_resultInterceptors[i].ResultSet, 1, $"protocol has {i} emitted result not once but {_resultInterceptors[i].ResultSet}");
//                Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminated");
            }

            bool? res = null;
            for (var i = 0; i < N; ++i)
            {
                if (_deliveryService._mutedPlayers.Contains(i)) continue;
                var ans = _resultInterceptors[i].Result;
                if (res == null)
                    res = ans;
                Assert.AreEqual(res, _resultInterceptors[i].Result);
            }
            Assert.True(used.Contains(res.Value));

            Console.Error.WriteLine("Result validated");

            for (var i = 0; i < N; ++i)
            {
                _broadcasts[i].Terminate();
                Console.Error.WriteLine($"boy {i} terminated");
                _broadcasts[i].WaitFinish();
                Console.Error.WriteLine($"boy {i} finished");
            }
            Console.Error.WriteLine("Ended protocols!");
        }

        [Test]
        [Repeat(100)]
        public void RandomTestLast41()
        {
            RunBinaryAgreementRandom(4, 1, DeliveryServiceMode.TAKE_LAST);
        }
        
        [Test]
        [Repeat(100)]
        public void RandomTestRandom41()
        {
            RunBinaryAgreementRandom(4, 1, DeliveryServiceMode.TAKE_RANDOM);
        }
        
        [Test]
        [Repeat(100)]
        public void RandomTestRandomWithRepeat41()
        {
            RunBinaryAgreementRandom(4, 1, DeliveryServiceMode.TAKE_RANDOM, 0, .2);
        }
        
        [Test]
        [Repeat(100)]
        public void RandomTestRandomWithMuted41()
        {
            RunBinaryAgreementRandom(4, 1, DeliveryServiceMode.TAKE_RANDOM, 1);
        }
        
        [Test]
        [Repeat(100)]
        public void RandomTestLastWithMuted41()
        {
            RunBinaryAgreementRandom(4, 1, DeliveryServiceMode.TAKE_LAST, 1);
        }
        
        [Test]
        [Repeat(100)]
        public void RandomTestLast72()
        {
            RunBinaryAgreementRandom(7, 2, DeliveryServiceMode.TAKE_LAST);
        }
        
        [Test]
        [Repeat(100)]
        public void RandomTestLastWithMuted72()
        {
            RunBinaryAgreementRandom(7, 2, DeliveryServiceMode.TAKE_LAST, 2);
        }
        
        [Test]
        [Repeat(100)]
        public void RandomTestLast103()
        {
            RunBinaryAgreementRandom(10, 3, DeliveryServiceMode.TAKE_LAST);
        }
        
        [Test]
        [Repeat(100)]
        public void RandomTestLastWithMuted103()
        {
            RunBinaryAgreementRandom(10, 3, DeliveryServiceMode.TAKE_LAST, 3);
        }
    }
}