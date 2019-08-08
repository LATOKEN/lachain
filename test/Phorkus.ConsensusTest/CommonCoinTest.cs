using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        private IConsensusBroadcaster[] _broadcasters;
        private PlayerSet _playerSet;
        private ProtocolInvoker<CoinId, bool>[] _resultInterceptors;

        [SetUp]
        public void SetUp()
        {
            Mcl.Init();
            var keygen = new TrustedKeyGen(N, F, new Random(0x0badfee0));
            var shares = keygen.GetPrivateShares().ToArray();
            var pubKeys = new PublicKeySet(shares.Select(share => share.GetPublicKeyShare()), F);
            _playerSet = new PlayerSet();
            _coins = new IConsensusProtocol[N];
            _broadcasters = new IConsensusBroadcaster[N];
            _resultInterceptors = new ProtocolInvoker<CoinId, bool>[N];
            for (uint i = 0; i < N; ++i)
            {
                _resultInterceptors[i] = new ProtocolInvoker<CoinId, bool>();
                _broadcasters[i] = new BroadcastSimulator(i, _playerSet);
                _coins[i] = new CommonCoin(
                    pubKeys, shares[i], new CoinId(0, 0, 0), _broadcasters[i]
                );
                _broadcasters[i].RegisterProtocols(new[] {_coins[i], _resultInterceptors[i]});
            }
        }

        [Test]
        public void TestAllHonest()
        {
            for (var i = 0; i < N; ++i)
            {
                _broadcasters[i].InternalRequest(
                    new ProtocolRequest<CoinId, object>(_resultInterceptors[i].Id, (CoinId) _coins[i].Id, null)
                );
            }

            for (var i = 0; i < N; ++i)
            {
                _coins[i].WaitFinish();
            }

            var results = new bool[N];
            for (var i = 0; i < N; ++i)
            {
                Assert.IsTrue(_coins[i].Terminated, $"protocol {i} did not terminate");
                Assert.AreEqual(_resultInterceptors[i].ResultSet, 1, $"protocol {i} emitted result not once");
                results[i] = _resultInterceptors[i].Result;
            }

            Assert.AreEqual(results.Distinct().Count(), 1, "all guys should get same coin");
        }
    }
}