using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Phorkus.Consensus;
using Phorkus.Consensus.BinaryAgreement;
using Phorkus.Consensus.CommonCoin.ThresholdSignature;
using Phorkus.Consensus.CommonSubset;
using Phorkus.Consensus.Messages;
using Phorkus.Consensus.TPKE;
using Phorkus.Crypto.MCL.BLS12_381;
using Phorkus.Utility.Utils;

namespace Phorkus.ConsensusTest
{
    [TestFixture]
    public class CommonSubsetTest 
    {
        private DeliverySerivce _deliverySerivce;
        private IConsensusProtocol[] _broadcasts;
        private IConsensusBroadcaster[] _broadcasters;
        private ProtocolInvoker<CommonSubsetId, ISet<EncryptedShare>>[] _resultInterceptors;
        private const int N = 7;
        private const int F = 2;
        private IWallet[] _wallets;
        private Random _rnd;

        [SetUp]
        public void SetUp()
        {
            _rnd = new Random();
            Mcl.Init();
            _deliverySerivce = new DeliverySerivce();
            _broadcasts = new IConsensusProtocol[N];
            _broadcasters = new IConsensusBroadcaster[N];
            _resultInterceptors = new ProtocolInvoker<CommonSubsetId, ISet<EncryptedShare>>[N];
            _wallets = new IWallet[N];
            var keygen = new TrustedKeyGen(N, F, new Random(0x0badfee0));
            var shares = keygen.GetPrivateShares().ToArray();
            var pubKeys = new PublicKeySet(shares.Select(share => share.GetPublicKeyShare()), F);
            for (var i = 0; i < N; ++i)
            {
                _resultInterceptors[i] = new ProtocolInvoker<CommonSubsetId, ISet<EncryptedShare>>();
                _wallets[i] = new Wallet(N, F) {PrivateKeyShare = shares[i], PublicKeySet = pubKeys};
                _broadcasters[i] = new BroadcastSimulator(i, _wallets[i], _deliverySerivce, false);
            }
        }

        private void SetUpAllHonest()
        {
            for (uint i = 0; i < N; ++i)
            {
                _broadcasts[i] = new CommonSubset(new CommonSubsetId(10), _wallets[i], _broadcasters[i]);
                _broadcasters[i].RegisterProtocols(new[] {_broadcasts[i], _resultInterceptors[i]});
            }
        }

        private void CheckOutput(EncryptedShare[] inputs, ISet<EncryptedShare>[] outputs, ISet<int> faulty = null)
        {
            if (faulty == null)
                faulty = new HashSet<int>();
            
            Assert.True(faulty.Count <= F);

            var numberOfInputs = 0;
            for (var i = 0; i < N; ++i)
                if (!faulty.Contains(i) && inputs[i] != null)
                    numberOfInputs++;
            
            // unsufficient number of inputs
            if (numberOfInputs < N - F)
                Assert.Pass();

            EncryptedShare[] canon = null;

            // Validity
            for (var i = 0; i < N; ++i)
            {
                if (faulty.Contains(i)) continue;

                var set = outputs[i];

                foreach (var share in set)
                {
                    Assert.True(inputs.Contains(share));
                }

                Assert.True(set.Count >= N - F);
                
                var cnt = 0;
                foreach (var share in set)
                {
                    if (!faulty.Contains(share.Id))
                        cnt++;
                }

                Assert.True(cnt >= N - 2 * F);

                // Agreement -- all correct nodes output the same
                if (canon == null)
                    canon = set.ToArray();
                else
                {
                    Assert.True(canon.SequenceEqual(set.ToArray()));
                }
            }
            
        }

        [Test]
        public void TestAllCommonSubset()
        {
            SetUpAllHonest();
            
            var inputs = new List<EncryptedShare>();
            for (var i = 0; i < N; ++i)
            {
//                var share = (i == 0) ? new EncryptedShare(G1.Zero, new byte[0], G2.Zero, i) : null;
                var share = new EncryptedShare(G1.Zero, new byte[0], G2.Zero, i);
                inputs.Add(share);
                _broadcasters[i].InternalRequest(new ProtocolRequest<CommonSubsetId, EncryptedShare>(
                    _resultInterceptors[i].Id, _broadcasts[i].Id as CommonSubsetId, share
                ));
            }

            for (var i = 0; i < N; ++i)
            {
                _broadcasts[i].WaitFinish();
            }

            var outputs = new List<ISet<EncryptedShare>>();

            for (var i = 0; i < N; ++i)
            {
                Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminated");
                Assert.AreEqual(_resultInterceptors[i].ResultSet, 1, $"protocol {i} has emitted result not once but {_resultInterceptors[i].ResultSet}");
                Assert.AreEqual(N, _resultInterceptors[i].Result.Count);
                
                outputs.Add(_resultInterceptors[i].Result);
            }
            
            CheckOutput(inputs.ToArray(), outputs.ToArray());
        }
    }
}