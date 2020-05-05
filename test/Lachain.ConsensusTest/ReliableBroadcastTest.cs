using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Lachain.Consensus;
using Lachain.Consensus.Messages;
using Lachain.Consensus.ReliableBroadcast;
using Lachain.Crypto.MCL.BLS12_381;
using Lachain.Crypto.TPKE;
using Lachain.Proto;

namespace Lachain.ConsensusTest
{
    [TestFixture]
    public class ReliableBroadcastTest
    {
        [SetUp]
        public void SetUp()
        {
            _deliveryService = new DeliveryService();
            _broadcasts = new IConsensusProtocol[N];
            _broadcasters = new IConsensusBroadcaster[N];
            _resultInterceptors = new ProtocolInvoker<ReliableBroadcastId, EncryptedShare>[N];
            _wallets = new IPrivateConsensusKeySet[N];

            _publicKeys = new PublicConsensusKeySet(N, F, null, null, Enumerable.Empty<ECDSAPublicKey>());
            Mcl.Init();
            for (var i = 0; i < N; ++i)
            {
                _wallets[i] = TestUtils.EmptyWallet(N, F);
                _broadcasters[i] = new BroadcastSimulator(i, _publicKeys, _wallets[i], _deliveryService, false);
                _resultInterceptors[i] = new ProtocolInvoker<ReliableBroadcastId, EncryptedShare>();
            }

            _rnd = new Random();
            var rnd = new byte[5000];
            _rnd.NextBytes(rnd);
            _testShare = new EncryptedShare(G1.Generator, rnd, G2.Generator, sender);
        }

        
        private const int N = 22;
        private const int F = 5;
        private readonly int sender = 0;

        private DeliveryService _deliveryService;
        private IConsensusProtocol[] _broadcasts;
        private IConsensusBroadcaster[] _broadcasters;
        private ProtocolInvoker<ReliableBroadcastId, EncryptedShare>[] _resultInterceptors;
        private IPrivateConsensusKeySet[] _wallets;
        private IPublicConsensusKeySet _publicKeys;
        private EncryptedShare _testShare;
        private Random _rnd;
        

        private void SetUpAllHonest()
        {
            for (uint i = 0; i < N; ++i)
            {
                _broadcasts[i] =
                    new ReliableBroadcast(new ReliableBroadcastId(sender, 0), _publicKeys, _broadcasters[i]);
                _broadcasters[i].RegisterProtocols(new[] {_broadcasts[i], _resultInterceptors[i]});
            }
        }

        private void SetupSomeSilent(List<int> silentID)
        {
            var random = new Random();
            var cnt = 0;
            while (cnt < F)
            {
                var x = random.Next() % N;
                if (_broadcasts[x] != null) continue;
                _broadcasts[x] = new SilentProtocol<ReliableBroadcastId>(new ReliableBroadcastId(0, 0));
                silentID.Add(x);
                ++cnt;
            }
            for (uint i = 0; i < N; ++i)
            {
                if (_broadcasts[i] == null)
                    _broadcasts[i] = 
                        new ReliableBroadcast(new ReliableBroadcastId(sender, 0), _publicKeys, _broadcasters[i]);
                _broadcasters[i].RegisterProtocols(new[] {_broadcasts[i], _resultInterceptors[i]});
            }
        }
        
        public void RunSetUpSomeDelay(DeliveryServiceMode mode=DeliveryServiceMode.TAKE_RANDOM, int muteCnt = 0, double repeatProbability = .0)
        {
            SetUpAllHonest();
            _deliveryService.RepeatProbability = repeatProbability; // вероятность повтора индивидуального сообщения
            _deliveryService.Mode = mode; // порядок доставки сообщения
            var rnd = new Random();
            var mutePlayers = new List<int>();
            while (_deliveryService._mutedPlayers.Count < muteCnt)
            {
                var tmp = rnd.Next(0, N - 1);
                _deliveryService.MutePlayer(tmp);
                mutePlayers.Add(tmp);
            }
            for (var i = 0; i < N; ++i)
            {
                _broadcasters[i].InternalRequest(new ProtocolRequest<ReliableBroadcastId, EncryptedShare>(
                    _resultInterceptors[i].Id, _broadcasts[i].Id as ReliableBroadcastId, i == sender ? _testShare : null
                ));
            }
            for (var i = 0; i < N; ++i)
            {
                if(!mutePlayers.Contains(i))
                    _broadcasts[i].WaitFinish();
            }
            for (var i = 0; i < N; ++i)
            {
                if(!mutePlayers.Contains(i))
                    Assert.AreEqual(_testShare, _resultInterceptors[i].Result);
            }

            for (var i = 0; i < N; ++i)
            {
                if(!mutePlayers.Contains(i))
                    Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminated");
            }   
        }
        
        [Test]
        public void RunSetUpHonest()
        {
            SetUpAllHonest();
            for (var i = 0; i < N; ++i)
            {
                _broadcasters[i].InternalRequest(new ProtocolRequest<ReliableBroadcastId, EncryptedShare>(
                    _resultInterceptors[i].Id, _broadcasts[i].Id as ReliableBroadcastId, i == sender ? _testShare : null
                ));
            }
            for (var i = 0; i < N; ++i) _broadcasts[i].WaitFinish();
            for (var i = 0; i < N; ++i) Assert.AreEqual(_testShare, _resultInterceptors[i].Result);
            for (var i = 0; i < N; ++i) Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminated");
        }
        [Test]
        public void RunSomeSilent()
        {
            var SilentID = new List<int>();
            SetupSomeSilent(SilentID);
            for (var i = 0; i < N; ++i)
            {
                _broadcasters[i].InternalRequest(new ProtocolRequest<ReliableBroadcastId, EncryptedShare>(
                    _resultInterceptors[i].Id, _broadcasts[i].Id as ReliableBroadcastId, i == sender ? _testShare : null
                ));
            }
            for (var i = 0; i < N; ++i) _broadcasts[i].WaitFinish();
            
            // Check true share only for NOT silent players
            for (var i = 0; i < N; ++i)
            {   
                if(!SilentID.Contains(i))
                    Assert.AreEqual(_testShare, _resultInterceptors[i].Result);
            }
            for (var i = 0; i < N; ++i) Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminated");
        }

        [Test]
        public void RunSetUpSomeDelay1()
        {
            RunSetUpSomeDelay(DeliveryServiceMode.TAKE_FIRST);
        }
        [Test]
        public void RunSetUpSomeDelay2()
        {
            RunSetUpSomeDelay(DeliveryServiceMode.TAKE_LAST);
        }
        [Test]
        public void RunSetUpSomeDelay3()
        {
            
            RunSetUpSomeDelay(DeliveryServiceMode.TAKE_RANDOM, 0, 0.2);
        }
        [Test]
        public void RunSetUpSomeDelay4()
        {
            
            RunSetUpSomeDelay(DeliveryServiceMode.TAKE_RANDOM, 0, 0.5);
        }        
        [Test]
        public void RunSetUpSomeDelay5()
        {   
            RunSetUpSomeDelay(DeliveryServiceMode.TAKE_RANDOM, 0, 0.9);
        }
        [Test]
        public void RunSetUpSomeDelayAndMute()
        {   
            RunSetUpSomeDelay(DeliveryServiceMode.TAKE_RANDOM, 5, 0.5);
        }
    }
}