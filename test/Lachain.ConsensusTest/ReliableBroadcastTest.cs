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

        private void SetUpAllHonest()
        {
            for (uint i = 0; i < N; ++i)
            {
                _broadcasts[i] =
                    new ReliableBroadcast(new ReliableBroadcastId(sender, 0), _publicKeys, _broadcasters[i]);
                _broadcasters[i].RegisterProtocols(new[] {_broadcasts[i], _resultInterceptors[i]});
            }
        }

        [Test]
        public void Run()
        {
            var share = new EncryptedShare(G1.Generator, new byte[]{1, 2, 3, 4, 5, 6}, G2.Generator, sender);
            SetUpAllHonest();
            for (var i = 0; i < N; ++i)
            {
                _broadcasters[i].InternalRequest(new ProtocolRequest<ReliableBroadcastId, EncryptedShare>(
                    _resultInterceptors[i].Id, _broadcasts[i].Id as ReliableBroadcastId, i == sender ? share : null
                    //_resultInterceptors[i].Id, _broadcasts[i].Id as ReliableBroadcastId, i == sender ? share : share
                ));
            }
            
            for (var i = 0; i < N; ++i) _broadcasts[i].WaitFinish();
            for (var i = 0; i < N; ++i)
            {
                Assert.AreEqual(share, _resultInterceptors[i].Result);
            }
            
            for (var i = 0; i < N; ++i) Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminated");
            
        }
    }
}