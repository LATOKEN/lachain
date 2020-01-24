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
        private DeliveryService _deliveryService;
        private IConsensusProtocol[] _acs;
        private IConsensusBroadcaster[] _broadcasters;
        private ProtocolInvoker<CommonSubsetId, ISet<EncryptedShare>>[] _resultInterceptors;
        private int N = 7;
        private int F = 2;
        private IWallet[] _wallets;
        private Random _rnd;

        private void SetUpAllHonest()
        {
            _rnd = new Random();
            Mcl.Init();
            _deliveryService = new DeliveryService();
            _acs = new IConsensusProtocol[N];
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
                _broadcasters[i] = new BroadcastSimulator(i, _wallets[i], _deliveryService, false);
            }
            
            for (uint i = 0; i < N; ++i)
            {
                _acs[i] = new CommonSubset(new CommonSubsetId(10), _wallets[i], _broadcasters[i]);
                _broadcasters[i].RegisterProtocols(new[] {_acs[i], _resultInterceptors[i]});
            }
        }
        
        // private void SetUpReliableBroadcast()
        // {        
        //     _deliveryService = new DeliveryService();
        //     _broadcasts = new IConsensusProtocol[N];
        //     _broadcasters = new IConsensusBroadcaster[N];
        //     _resultInterceptors = new ProtocolInvoker<ReliableBroadcastId, EncryptedShare>[N];
        //     _rnd = new Random();
        //     _wallets = new IWallet[N];
        //     
        //     Mcl.Init();
        //     for (var i = 0; i < N; ++i)
        //     {
        //         _wallets[i] = new Wallet(N, F);
        //         _broadcasters[i] = new BroadcastSimulator(i, _wallets[i], _deliveryService, mixMessages: false);
        //         _resultInterceptors[i] = new ProtocolInvoker<ReliableBroadcastId, EncryptedShare>();
        //     }
        // }
        // private void SetUpAllHonest()
        // {
        //     for (uint i = 0; i < N; ++i)
        //     {
        //         var sender = 0;
        //         _broadcasts[i] = new ReliableBroadcast.ReliableBroadcast(new ReliableBroadcastId(sender, 0), _wallets[i], _broadcasters[i]);
        //         _broadcasters[i].RegisterProtocols(new[] {_broadcasts[i], _resultInterceptors[i]});
        //     }
        // }

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

        public void TestAllCommonSubset(int n, DeliveryServiceMode mode = DeliveryServiceMode.TAKE_FIRST)
        {
            N = n;
            SetUpAllHonest();
            _deliveryService.Mode = mode;
            
            Console.Error.WriteLine("------------------------------------------------------------------- NEW ITERATION ------------------------------------------------------------------------------------------------------------------------------------------------------");
            
            var inputs = new List<EncryptedShare>();
            for (var i = 0; i < N; ++i)
            {
//                var share = (i == 0) ? new EncryptedShare(G1.Zero, new byte[0], G2.Zero, i) : null;
                var share = new EncryptedShare(G1.Zero, new byte[0], G2.Zero, i);
                inputs.Add(share);
                _broadcasters[i].InternalRequest(new ProtocolRequest<CommonSubsetId, EncryptedShare>(
                    _resultInterceptors[i].Id, _acs[i].Id as CommonSubsetId, share
                ));
            }

            for (var i = 0; i < N; ++i)
            {
                if (_deliveryService._mutedPlayers.Contains(i)) continue;
                _acs[i].WaitResult();
            }
            Console.Error.WriteLine("All players produced result");
            _deliveryService.WaitFinish();
            Console.Error.WriteLine("Delivery service shut down");
            for (var i = 0; i < N; ++i)
            {
                _broadcasters[i].Terminate();
                foreach (var id in _wallets[i].ProtocolIds)
                {
                    Assert.NotNull(id);
                    var protocol = _broadcasters[i].Registry.GetValueOrDefault(id);
                    if (protocol == null)
                    {
                        Console.Error.WriteLine($"Didn't found protocol with id {id}'");
                        continue;
                    }
                    protocol.Terminate();
                    Console.Error.WriteLine($"boy {id} terminated");
                    protocol.WaitFinish();
                    Console.Error.WriteLine($"boy {id} finished");
                }
            }
            Console.Error.WriteLine("Ended protocols!");

            var outputs = new List<ISet<EncryptedShare>>();

            for (var i = 0; i < N; ++i)
            {
                Assert.IsTrue(_acs[i].Terminated, $"protocol {i} did not terminated");
                Assert.AreEqual(_resultInterceptors[i].ResultSet, 1, $"protocol {i} has emitted result not once but {_resultInterceptors[i].ResultSet}");
                Assert.AreEqual(N, _resultInterceptors[i].Result.Count);
                
                outputs.Add(_resultInterceptors[i].Result);
            }
            
            CheckOutput(inputs.ToArray(), outputs.ToArray());
        }

        [Test]
        [Repeat(10)]
        public void TestSimple7()
        {
            TestAllCommonSubset(7);
        }
        
        [Test]
        [Repeat(10)]
        public void TestRandom7()
        {
            TestAllCommonSubset(7, DeliveryServiceMode.TAKE_RANDOM);
        }
    }
}