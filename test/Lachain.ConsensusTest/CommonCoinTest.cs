using System;
using System.Linq;
using NUnit.Framework;
using Lachain.Consensus;
using Lachain.Consensus.CommonCoin;
using Lachain.Consensus.Messages;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Proto;

namespace Lachain.ConsensusTest
{
    public class CommonCoinTest
    {
        private readonly Random _rnd = new Random();
        private IConsensusBroadcaster[] _broadcasters = null!;
        private IConsensusProtocol[] _coins = null!;
        private DeliveryService _deliveryService = null!;
        private IPublicConsensusKeySet _publicKeys = null!;
        private ProtocolInvoker<CoinId, CoinResult>[] _resultInterceptors = null!;
        private IPrivateConsensusKeySet[] _wallets = null!;

        public void SetUp(int n, int f)
        {
            var keygen = new TrustedKeyGen(n, f);
            var shares = keygen.GetPrivateShares().ToArray();
            var pubKeys = new PublicKeySet(shares.Select(share => share.GetPublicKeyShare()), f);
            _deliveryService = new DeliveryService();
            _coins = new IConsensusProtocol[n];
            _broadcasters = new IConsensusBroadcaster[n];
            _resultInterceptors = new ProtocolInvoker<CoinId, CoinResult>[n];
            _wallets = new IPrivateConsensusKeySet[n];
            _publicKeys = new PublicConsensusKeySet(n, f, null!,new Crypto.TPKE.PublicKey[]{}, pubKeys, Enumerable.Empty<ECDSAPublicKey>());
            for (var i = 0; i < n; ++i)
            {
                _resultInterceptors[i] = new ProtocolInvoker<CoinId, CoinResult>();
                _wallets[i] = new PrivateConsensusKeySet(null!, null!, shares[i]);
                _broadcasters[i] = new BroadcastSimulator(i, _publicKeys, _wallets[i], _deliveryService, false);
                _coins[i] = new CommonCoin(
                    new CoinId(0, 0, 0), _publicKeys, shares[i], _broadcasters[i]
                );
                _broadcasters[i].RegisterProtocols(new[] {_coins[i], _resultInterceptors[i]});
            }
        }

        private void Run(int n, int f)
        {
            for (var i = 0; i < n; ++i)
                _broadcasters[i].InternalRequest(
                    new ProtocolRequest<CoinId, object?>(_resultInterceptors[i].Id, (CoinId) _coins[i].Id, null)
                );

            for (var i = 0; i < n; ++i) _coins[i].WaitFinish();

            _deliveryService.WaitFinish();

            var results = new CoinResult[n];
            for (var i = 0; i < n; ++i)
            {
                Assert.IsTrue(_coins[i].Terminated, $"protocol {i} did not terminate");
                Assert.AreEqual(_resultInterceptors[i].ResultSet, 1, $"protocol {i} emitted result not once");
                results[i] = _resultInterceptors[i].Result;
            }

            Assert.AreEqual(1, results.Distinct().Count(), "all guys should get same coin");
        }

        [Test]
        [Repeat(10)]
        public void TestAllHonest()
        {
            var n = _rnd.Next(1, 10);
            var f = _rnd.Next((n - 1) / 3 + 1);
            SetUp(n, f);
            Run(n, f);
        }

        [Test]
        [Repeat(100)]
        public void TestAllHonestWithRepeat()
        {
            var n = _rnd.Next(1, 10);
            var f = _rnd.Next((n - 1) / 3 + 1);
            SetUp(n, f);
            _deliveryService.RepeatProbability = 0.9;
            Run(n, f);
        }
    }
}