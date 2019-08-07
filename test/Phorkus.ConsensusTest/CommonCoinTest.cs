using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Phorkus.Consensus;
using Phorkus.Consensus.CommonCoin;
using Phorkus.Consensus.CommonCoin.ThresholdCrypto;
using Phorkus.Consensus.Messages;
using Phorkus.Crypto.MCL.BLS12_381;

namespace Phorkus.ConsensusTest
{
    public class CommonCoinTest
    {
        private const int N = 7, F = 2;
        private IConsensusProtocol[] _coins;
        private CoinBroadcaster[] _broadcasters;
        private Thread[] _threads;
        private PlayerSet _playerSet;

        public class CoinBroadcaster : BroadcastSimulator
        {
            public bool? Result;
            public CoinBroadcaster(uint sender, PlayerSet playerSet) : base(sender, playerSet)
            {
            }

            public void InternalResponse(ProtocolResult<CoinId, bool> result)
            {
                base.InternalResponse(result);
                Result = result.Result;
            }
        }

        [SetUp]
        public void SetUp()
        {
            Mcl.Init();
            var keygen = new TrustedKeyGen(N, F, new Random(0x0badfee0));
            var shares = keygen.GetPrivateShares().ToArray();
            var pubKeys = new PublicKeySet(shares.Select(share => share.GetPublicKeyShare()), F);
            _playerSet = new PlayerSet();
            _coins = new IConsensusProtocol[N];
            _broadcasters = new CoinBroadcaster[N];
            _threads = new Thread[N];
            for (uint i = 0; i < N; ++i)
            {
                _broadcasters[i] = new CoinBroadcaster(i, _playerSet);
                _coins[i] = new CommonCoin(
                    pubKeys, shares[i], new CoinId(0, 0, 0), _broadcasters[i]
                );
                _broadcasters[i].RegisterProtocols(new[] {_coins[i]});
                var copyOfI = i;
                _threads[i] = new Thread(() =>
                {
                    Thread.CurrentThread.IsBackground = true;
                    _coins[copyOfI].Start();
                });
                _threads[i].Start();
            }
        }

        [Test]
        public void TestAllHonest()
        {
            for (var i = 0; i < N; ++i)
            {
                _broadcasters[i].InternalRequest(new ProtocolRequest<CoinId, object>(null, (CoinId) _coins[i].Id, null));
            }

            for (var i = 0; i < N; ++i)
            {
                _threads[i].Join();
            }

            var results = new bool[N];
            for (var i = 0; i < N; ++i)
            {
                Assert.IsTrue(_coins[i].Terminated, $"protocol {i} did not terminate");
                Assert.NotNull(_broadcasters[i].Result, $"protocol {i} did not emit result");
                results[i] = (bool) _broadcasters[i].Result;
            }

            Assert.AreEqual(results.Distinct().Count(), 1, "all guys should get same coin");
        }
    }
}