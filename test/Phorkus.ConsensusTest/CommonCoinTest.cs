using System.Linq;
using NUnit.Framework;
using Phorkus.Consensus;
using Phorkus.Consensus.CommonCoin;
using Phorkus.Consensus.Messages;
using Phorkus.Crypto.MCL.BLS12_381;
using Phorkus.Crypto.ThresholdSignature;

namespace Phorkus.ConsensusTest
{
    public class CommonCoinTest
    {
        private const int N = 7, F = 2;
        private IConsensusProtocol[] _coins;
        private IConsensusBroadcaster[] _broadcasters;
        private DeliveryService _deliveryService;
        private ProtocolInvoker<CoinId, bool>[] _resultInterceptors;
        private IWallet[] _wallets;

        public void SetUp()
        {
            Mcl.Init();
            var keygen = new TrustedKeyGen(N, F);
            var shares = keygen.GetPrivateShares().ToArray();
            var pubKeys = new PublicKeySet(shares.Select(share => share.GetPublicKeyShare()), F);
            _deliveryService = new DeliveryService();
            _coins = new IConsensusProtocol[N];
            _broadcasters = new IConsensusBroadcaster[N];
            _resultInterceptors = new ProtocolInvoker<CoinId, bool>[N];
            _wallets = new IWallet[N];
            for (var i = 0; i < N; ++i)
            {
                _resultInterceptors[i] = new ProtocolInvoker<CoinId, bool>();
                _wallets[i] = new Wallet(N, F);
                _wallets[i].ThresholdSignaturePrivateKeyShare = shares[i];
                _wallets[i].ThresholdSignaturePublicKeySet = pubKeys;
                _broadcasters[i] = new BroadcastSimulator(i, _wallets[i], _deliveryService, false);
                _coins[i] = new CommonCoin(
                    new CoinId(0, 0, 0), _wallets[i], _broadcasters[i]
                );
                _broadcasters[i].RegisterProtocols(new[] {_coins[i], _resultInterceptors[i]});
            }
        }

        public void Run()
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
            _deliveryService.WaitFinish();

            var results = new bool[N]; // bit vector to reach agreement
            for (var i = 0; i < N; ++i)
            {
                Assert.IsTrue(_coins[i].Terminated, $"protocol {i} did not terminate");
                Assert.AreEqual(_resultInterceptors[i].ResultSet, 1, $"protocol {i} emitted result not once");
                results[i] = _resultInterceptors[i].Result;
            }

            Assert.AreEqual(results.Distinct().Count(), 1, "all guys should get same coin");
        }

        [Test]
        [Repeat(100)]
        public void TestAllHonest()
        {
            SetUp();
            Run();
        }

        [Test]
        [Repeat(100)]
        public void TestAllHonestWithRepeat()
        {
            SetUp();
            _deliveryService.RepeatProbability = 0.9;
            Run();
        }
    }
}