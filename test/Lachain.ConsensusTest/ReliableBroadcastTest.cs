using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using NUnit.Framework;
using Lachain.Consensus;
using Lachain.Consensus.Messages;
using Lachain.Consensus.ReliableBroadcast;
using Lachain.Crypto.TPKE;
using Lachain.Proto;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using MCL.BLS12_381.Net;

namespace Lachain.ConsensusTest
{
    [TestFixture]
    public class ReliableBroadcastTest
    {
        private const int Sender = 0;
        private readonly Random _rnd = new Random();

        private DeliveryService _deliveryService = null!;
        private IConsensusProtocol[] _broadcasts = null!;
        private IConsensusBroadcaster[] _broadcasters = null!;
        private ProtocolInvoker<ReliableBroadcastId, EncryptedShare>[] _resultInterceptors = null!;
        private IPrivateConsensusKeySet[] _wallets = null!;
        private IPublicConsensusKeySet _publicKeys = null!;
        private EncryptedShare _testShare = null!;

        private void SetUp(int n, int f)
        {
            _deliveryService = new DeliveryService();
            _broadcasts = new IConsensusProtocol[n];
            _broadcasters = new IConsensusBroadcaster[n];
            _resultInterceptors = new ProtocolInvoker<ReliableBroadcastId, EncryptedShare>[n];
            _wallets = new IPrivateConsensusKeySet[n];
            _publicKeys = new PublicConsensusKeySet(
                n, f, null!, null!, null!,
                Enumerable.Range(0, n)
                    .Select(i => new ECDSAPublicKey {Buffer = ByteString.CopyFrom(i.ToBytes().ToArray())})
            );
            for (var i = 0; i < n; ++i)
            {
                _wallets[i] = TestUtils.EmptyWallet(n, f);
                _broadcasters[i] = new BroadcastSimulator(i, _publicKeys, _wallets[i], _deliveryService, false);
                _resultInterceptors[i] = new ProtocolInvoker<ReliableBroadcastId, EncryptedShare>();
            }

            var bytes = Enumerable.Range(0, 32)
                .Select(x => (byte) (x * x * 0))
                .ToArray();
            _testShare = new EncryptedShare(G1.Generator, bytes, G2.Generator, Sender);
        }


        private void SetUpAllHonest(int n, int f)
        {
            SetUp(n, f);
            for (var i = 0; i < n; ++i)
            {
                _broadcasts[i] =
                    new ReliableBroadcast(new ReliableBroadcastId(Sender, 0), _publicKeys, _broadcasters[i]);
                _broadcasters[i].RegisterProtocols(new[] {_broadcasts[i], _resultInterceptors[i]});
            }
        }

        private void SetUpAllHonestNSenders(int n, int f)
        {
            SetUp(n, f);
            for (var i = 0; i < n; ++i)
            {
                _broadcasts[i] =
                    new ReliableBroadcast(new ReliableBroadcastId(i, 0), _publicKeys, _broadcasters[i]);
                _broadcasters[i].RegisterProtocols(new[] {_broadcasts[i], _resultInterceptors[i]});
            }
        }

        private void SetupSomeSilent(int n, int f, ICollection<int> silentId)
        {
            SetUp(n, f);
            var cnt = 0;
            while (cnt < f)
            {
                var x = _rnd.Next(n);
                if (x == 0) continue;
                if (_broadcasts[x] != null) continue;
                _broadcasts[x] = new SilentProtocol<ReliableBroadcastId>(new ReliableBroadcastId(0, 0));
                silentId.Add(x);
                ++cnt;
            }

            for (uint i = 0; i < n; ++i)
            {
                _broadcasts[i] ??=
                    new ReliableBroadcast(new ReliableBroadcastId(Sender, 0), _publicKeys, _broadcasters[i]);
                _broadcasters[i].RegisterProtocols(new[] {_broadcasts[i], _resultInterceptors[i]});
            }
        }

        private void RunNSenders(
            int n, int f,
            DeliveryServiceMode mode = DeliveryServiceMode.TAKE_RANDOM,
            double repeatProbability = .0
        )
        {
            SetUpAllHonestNSenders(n, f);
            _deliveryService.RepeatProbability = repeatProbability;
            _deliveryService.Mode = mode;
            var rnd = new Random();
            var mutePlayers = new List<int>();
            while (_deliveryService.MutedPlayers.Count < f)
            {
                var tmp = rnd.Next(0, n);
                _deliveryService.MutePlayer(tmp);
                mutePlayers.Add(tmp);
            }

            for (var i = 0; i < n; ++i)
            {
                _broadcasters[i].InternalRequest(new ProtocolRequest<ReliableBroadcastId, EncryptedShare?>
                    (_resultInterceptors[i].Id, new ReliableBroadcastId(i, 0), _testShare));

                for (var j = 0; j < n; ++j)
                {
                    if (j == i) continue;
                    _broadcasters[i].InternalRequest(new ProtocolRequest<ReliableBroadcastId, EncryptedShare?>
                        (_resultInterceptors[i].Id, new ReliableBroadcastId(j, 0), null));
                }
            }


            for (var i = 0; i < n; ++i)
            {
                if (!mutePlayers.Contains(i))
                    _broadcasts[i].WaitFinish();
            }

            for (var i = 0; i < n; ++i)
            {
                if (!mutePlayers.Contains(i))
                    Assert.AreEqual(_testShare, _resultInterceptors[i].Result);
            }

            for (var i = 0; i < n; ++i)
            {
                if (!mutePlayers.Contains(i))
                    Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminate");
            }
        }

        public void RunOneSender(
            int n, int f,
            DeliveryServiceMode mode = DeliveryServiceMode.TAKE_RANDOM,
            double repeatProbability = .0
        )
        {
            SetUpAllHonest(n, f);
            _deliveryService.RepeatProbability = repeatProbability;
            _deliveryService.Mode = mode;
            var rnd = new Random();
            var mutePlayers = new List<int>();
            while (_deliveryService.MutedPlayers.Count < f)
            {
                var tmp = rnd.Next(n);
                _deliveryService.MutePlayer(tmp);
                mutePlayers.Add(tmp);
            }

            for (var i = 0; i < n; ++i)
            {
                _broadcasters[i].InternalRequest(new ProtocolRequest<ReliableBroadcastId, EncryptedShare?>(
                    _resultInterceptors[i].Id, (_broadcasts[i].Id as ReliableBroadcastId)!,
                    i == Sender ? _testShare : null
                ));
            }

            for (var i = 0; i < n; ++i)
            {
                if (!mutePlayers.Contains(i))
                    _broadcasts[i].WaitFinish();
            }

            for (var i = 0; i < n; ++i)
            {
                if (!mutePlayers.Contains(i))
                    Assert.AreEqual(_testShare, _resultInterceptors[i].Result);
            }

            for (var i = 0; i < n; ++i)
            {
                if (!mutePlayers.Contains(i))
                    Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminated");
            }
        }

        [Test]
        public void TestOneSender_7_0()
        {
            const int n = 7;
            SetUpAllHonest(n, 0);
            for (var i = 0; i < n; ++i)
            {
                _broadcasters[i].InternalRequest(new ProtocolRequest<ReliableBroadcastId, EncryptedShare?>(
                    _resultInterceptors[i].Id, (_broadcasts[i].Id as ReliableBroadcastId)!,
                    i == Sender ? _testShare : null
                ));
            }

            for (var i = 0; i < n; ++i) _broadcasts[i].WaitFinish();
            for (var i = 0; i < n; ++i)
            {
                Assert.AreEqual(_testShare, _resultInterceptors[i].Result);
            }

            for (var i = 0; i < n; ++i) Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminate");
        }

        [Test]
        public void TestNSenders_7_0()
        {
            const int n = 7;
            SetUpAllHonestNSenders(n, 0);
            for (var i = 0; i < n; ++i)
            {
                _broadcasters[i].InternalRequest(new ProtocolRequest<ReliableBroadcastId, EncryptedShare?>
                    (_resultInterceptors[i].Id, new ReliableBroadcastId(i, 0), _testShare));

                for (var j = 0; j < n; ++j)
                {
                    if (j != i)
                    {
                        _broadcasters[i].InternalRequest(new ProtocolRequest<ReliableBroadcastId, EncryptedShare?>
                            (_resultInterceptors[i].Id, new ReliableBroadcastId(j, 0), null));
                    }
                }
            }

            for (var i = 0; i < n; ++i) _broadcasts[i].WaitFinish();
            for (var i = 0; i < n; ++i)
            {
                Assert.AreEqual(_testShare, _resultInterceptors[i].Result);
            }

            for (var i = 0; i < n; ++i) Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminated");
        }

        [Test]
        [Timeout(5000)]
        public void TestOneDealerSomeSilent_7_2()
        {
            const int n = 7, f = 2;
            var silentId = new List<int>();
            SetupSomeSilent(n, f, silentId);
            for (var i = 0; i < n; ++i)
            {
                _broadcasters[i].InternalRequest(new ProtocolRequest<ReliableBroadcastId, EncryptedShare?>(
                    _resultInterceptors[i].Id, (_broadcasts[i].Id as ReliableBroadcastId)!,
                    i == Sender ? _testShare : null
                ));
            }

            for (var i = 0; i < n; ++i) _broadcasts[i].WaitFinish();
            for (var i = 0; i < n; ++i)
            {
                // Check true share only for NOT silent players
                if (!silentId.Contains(i))
                {
                    Assert.AreEqual(_testShare, _resultInterceptors[i].Result);
                }
            }

            for (var i = 0; i < n; ++i) Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminate");
        }

        // [Test]
        // //[Timeout(5000)]
        // [Repeat(300)]
        // public void TestAllDealerSomeSilent_7_2()
        // {
        //     const int n = 7, f = 2;
        //     var silentId = new List<int>();
        //     SetupSomeSilent(n, f, silentId);
        //
        //     for (var j = 0; j < n; ++j)
        //     {
        //         _broadcasters[j].InternalRequest(
        //             new ProtocolRequest<ReliableBroadcastId, EncryptedShare?>(_resultInterceptors[j].Id, new ReliableBroadcastId(j, 0), _testShare)
        //             );
        //         
        //         for (var i = 0; i < n; ++i)
        //         {
        //             if (i != j)
        //             {
        //                 _broadcasters[j].InternalRequest(
        //                     new ProtocolRequest<ReliableBroadcastId, EncryptedShare?>(
        //                         _resultInterceptors[j].Id, new ReliableBroadcastId(i, 0), null 
        //                         )
        //                     );    
        //             }
        //             
        //         }    
        //     }
        //
        //     for (var i = 0; i < n; ++i) _broadcasts[i].WaitFinish();
        //     for (var i = 0; i < n; ++i)
        //     {
        //         // Check true share only for NOT silent players
        //         if (!silentId.Contains(i))
        //         {
        //             Assert.AreEqual(_testShare, _resultInterceptors[i].Result);
        //         }
        //             
        //     }
        //     for (var i = 0; i < n; ++i) Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminate");
        // }


        [Test]
        [Timeout(5000)]
        public void TestNSenders_TakeFirst_7_2()
        {
            const int n = 7, f = 2;
            RunNSenders(n, f, DeliveryServiceMode.TAKE_FIRST);
        }


        [Test]
        [Timeout(5000)]
        public void TestNSenders_TakeLast_7_2()
        {
            const int n = 7, f = 2;
            RunNSenders(n, f, DeliveryServiceMode.TAKE_LAST);
        }

        [Test]
        [Timeout(5000)]
        public void TestNSenders_TakeRandom_7_2()
        {
            const int n = 7, f = 2;
            RunNSenders(n, f);
        }

        [Test]
        [Timeout(5000)]
        public void TestNSenders_TakeFirst_WithRepeat_7_2()
        {
            const int n = 7, f = 2;
            RunNSenders(n, f, DeliveryServiceMode.TAKE_FIRST, 0.8);
        }

        [Test]
        [Timeout(5000)]
        public void TestNSenders_TakeLast_WithRepeat_7_2()
        {
            const int n = 7, f = 2;
            RunNSenders(n, f, DeliveryServiceMode.TAKE_LAST, 0.8);
        }

        [Test]
        [Timeout(5000)]
        public void TestNSenders_TakeLast_WithRandom_7_2()
        {
            const int n = 7, f = 2;
            RunNSenders(n, f, DeliveryServiceMode.TAKE_RANDOM, 0.8);
        }

        [Test]
        [Timeout(5000)]
        [Repeat(5)]
        public void TestNSenders_FullRandom()
        {
            var n = _rnd.Next(10);
            var f = _rnd.Next((n - 1) / 3 + 1);
            var mode = _rnd.SelectRandom(Enum.GetValues(typeof(DeliveryServiceMode)).Cast<DeliveryServiceMode>());
            var prob = _rnd.NextDouble();
            RunNSenders(n, f, mode, prob);
        }
    }
}