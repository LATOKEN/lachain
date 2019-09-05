using System;
using System.Linq;
using NUnit.Framework;
using Phorkus.Consensus;
using Phorkus.Consensus.CommonCoin;
using Phorkus.Consensus.CommonCoin.ThresholdSignature;
using Phorkus.Consensus.Messages;
using Phorkus.Crypto.MCL.BLS12_381;

namespace Phorkus.ConsensusTest
{
    public class CommonCoinTest
    {
        private const int N = 7, F = 2;
        private IConsensusProtocol[] _coins;
        private IConsensusBroadcaster[] _broadcasters;
        private DeliverySerivce _deliverySerivce;
        private ProtocolInvoker<CoinId, bool>[] _resultInterceptors;
        private IWallet[] _wallets;

        [SetUp]
        public void SetUp()
        {
            Mcl.Init();
            var keygen = new TrustedKeyGen(N, F, new Random(0x0badfee0));
            var shares = keygen.GetPrivateShares().ToArray();
            var pubKeys = new PublicKeySet(shares.Select(share => share.GetPublicKeyShare()), F);
            _deliverySerivce = new DeliverySerivce();
            _coins = new IConsensusProtocol[N];
            _broadcasters = new IConsensusBroadcaster[N];
            _resultInterceptors = new ProtocolInvoker<CoinId, bool>[N];
            _wallets = new IWallet[N];
            for (var i = 0; i < N; ++i)
            {
                _resultInterceptors[i] = new ProtocolInvoker<CoinId, bool>();
                _wallets[i] = new Wallet(N, F);
                _wallets[i].PrivateKeyShare = shares[i];
                _wallets[i].PublicKeySet = pubKeys;
                _broadcasters[i] = new BroadcastSimulator(i, _wallets[i], _deliverySerivce, false);
                _coins[i] = new CommonCoin(
                    new CoinId(0, 0, 0), _wallets[i], _broadcasters[i]
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