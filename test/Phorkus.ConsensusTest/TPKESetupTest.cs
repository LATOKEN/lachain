using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Phorkus.Consensus;
using Phorkus.Consensus.BinaryAgreement;
using Phorkus.Consensus.Messages;
using Phorkus.Consensus.TPKE;
using Phorkus.Crypto.MCL.BLS12_381;
using Phorkus.Utility.Utils;

namespace Phorkus.ConsensusTest
{
    [TestFixture]
    public class TPKESetupTest
    {
        private PlayerSet _playerSet;
        private IConsensusProtocol[] _broadcasts;
        private IConsensusBroadcaster[] _broadcasters;
        private ProtocolInvoker<TPKESetupId, TPKEKeys>[] _resultInterceptors;
        private const int N = 10;
        private const int T = 5;

        [SetUp]
        public void SetUp()
        {
            _playerSet = new PlayerSet();
            _broadcasts = new IConsensusProtocol[N];
            _broadcasters = new IConsensusBroadcaster[N];
            _resultInterceptors = new ProtocolInvoker<TPKESetupId, TPKEKeys>[N];
            for (var i = 0; i < N; ++i)
            {
                _resultInterceptors[i] = new ProtocolInvoker<TPKESetupId, TPKEKeys>();
                _broadcasters[i] = new BroadcastSimulator(i, _playerSet);
            }
            
            Mcl.Init();
        }

        private void SetUpAllHonest()
        {
            for (uint i = 0; i < N; ++i)
            {
                _broadcasts[i] = new TPKESetup(N, T, new TPKESetupId(0), _broadcasters[i]);
                _broadcasters[i].RegisterProtocols(new[] {_broadcasts[i], _resultInterceptors[i]});
            }
        }

        [Test]
        [Repeat(100)]
        public void TestBinaryBroadcastAllZero()
        {
            var rnd = new Random();
            
            SetUpAllHonest();
            for (var i = 0; i < N; ++i)
            {
                _broadcasters[i].InternalRequest(new ProtocolRequest<TPKESetupId, object>(
                    _resultInterceptors[i].Id, _broadcasts[i].Id as TPKESetupId, null
                ));
            }

            for (var i = 0; i < N; ++i)
            {
                _broadcasts[i].WaitFinish();
            }

            for (var i = 0; i < N; ++i)
            {
                Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminated");
            }
            
            // test that pub key can be recovered correctly using interpolation
            ISet<int> S = new HashSet<int>();
            while (S.Count < T)
            {
                S.Add(rnd.Next(0, N - 1));
            }

            var ys = new List<G1>();
            var xs = new List<Fr>();

            foreach (var i in S)
            {
               xs.Add(Fr.FromInt(i + 1));
               ys.Add(_resultInterceptors[i].Result.PrivKey.Y);
            }

            var A = Mcl.LagrangeInterpolateG1(xs.ToArray(), ys.ToArray());
            var B = _resultInterceptors[0].Result.PubKey.Y;
//            Console.Error.WriteLine(B.GetStr(0));
            Assert.True(B.Equals(A), "interpolated pubkey equals to real pubkey");
        }
    }
}