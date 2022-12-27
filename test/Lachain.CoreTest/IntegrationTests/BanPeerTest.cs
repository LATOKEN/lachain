using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Google.Protobuf;
using Lachain.Consensus;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Hardfork;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Operations;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Blockchain.SystemContracts;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.SystemContracts.Interface;
using Lachain.Core.Blockchain.VM;
using Lachain.Core.CLI;
using Lachain.Core.Config;
using Lachain.Core.DI;
using Lachain.Core.DI.Modules;
using Lachain.Core.DI.SimpleInjector;
using Lachain.Core.Network;
using Lachain.Core.Vault;
using Lachain.Crypto;
using Lachain.Crypto.ECDSA;
using Lachain.Crypto.Misc;
using Lachain.Logger;
using Lachain.Networking;
using Lachain.Networking.PeerFault;
using Lachain.Proto;
using Lachain.Storage.Repositories;
using Lachain.Storage.State;
using Lachain.Utility;
using Lachain.Utility.Utils;
using Lachain.UtilityTest;
using NUnit.Framework;

namespace Lachain.CoreTest.IntegrationTests
{
    [TestFixture]
    public class BanPeerTest
    {
        private static readonly ILogger<BanPeerTest> Logger = LoggerFactory.GetLoggerForClass<BanPeerTest>();
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();

        private IBlockManager _blockManager = null!;
        private ITransactionPool _transactionPool = null!;
        private IStateManager _stateManager = null!;
        private IPrivateWallet _wallet = null!;
        private IConfigManager _configManager = null!;
        private IBannedPeerTracker _banTracker = null!;
        private IPeerBanRepository _banRepo = null!;
        private INetworkManager _networkManager = null!;
        private IContainer? _container;

        public BanPeerTest()
        {
           var containerBuilder = new SimpleInjectorContainerBuilder(new ConfigManager(
                Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.json"),
                new RunOptions()
            ));

            containerBuilder.RegisterModule<BlockchainModule>();
            containerBuilder.RegisterModule<ConfigModule>();
            containerBuilder.RegisterModule<StorageModule>();
            containerBuilder.RegisterModule<ConsensusModule>();
            containerBuilder.RegisterModule<NetworkModule>();

            _container = containerBuilder.Build();

        } 

        [SetUp]
        public void Setup()
        {
            _container?.Dispose() ;
            TestUtils.DeleteTestChainData();
            var containerBuilder = new SimpleInjectorContainerBuilder(new ConfigManager(
                Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.json"),
                new RunOptions()
            ));
            
            containerBuilder.RegisterModule<BlockchainModule>();
            containerBuilder.RegisterModule<ConfigModule>();
            containerBuilder.RegisterModule<StorageModule>();
            containerBuilder.RegisterModule<ConsensusModule>();
            containerBuilder.RegisterModule<NetworkModule>();
            _container = containerBuilder.Build();
            _blockManager = _container.Resolve<IBlockManager>();
            _stateManager = _container.Resolve<IStateManager>();
            _wallet = _container.Resolve<IPrivateWallet>();
            _transactionPool = _container.Resolve<ITransactionPool>();
            _configManager = _container.Resolve<IConfigManager>();
            _banTracker = _container.Resolve<IBannedPeerTracker>();
            _banRepo = _container.Resolve<IPeerBanRepository>();
            _networkManager = _container.Resolve<INetworkManager>();

            // set chainId from config
            if (TransactionUtils.ChainId(false) == 0)
            {
                var chainId = _configManager.GetConfig<NetworkConfig>("network")?.ChainId;
                var newChainId = _configManager.GetConfig<NetworkConfig>("network")?.NewChainId;
                TransactionUtils.SetChainId((int)chainId!, (int)newChainId!);
                HardforkHeights.SetHardforkHeights(_configManager.GetConfig<HardforkConfig>("hardfork") ?? throw new InvalidOperationException());
                StakingContract.Initialize(_configManager.GetConfig<NetworkConfig>("network")!);
            }    
        }

        [TearDown]
        public void Teardown()
        {
            _container?.Dispose();
            TestUtils.DeleteTestChainData();
        }

        [Test]
        public void Test_BanPeer()
        {
            var built = _blockManager.TryBuildGenesisBlock();
            _banTracker.Start();
            while (NetworkManagerBase.CycleNumber(_blockManager.GetHeight()) < 1)
            {
                var emptyBlock = BuildNextBlock();
                var exc = ExecuteBlock(emptyBlock);
                Assert.AreEqual(OperatingError.Ok, exc);
            }
            var randomPrivateKey = Crypto.GeneratePrivateKey().ToPrivateKey();
            VotePeer(randomPrivateKey.GetPublicKey().EncodeCompressed());
            CheckBan(1, true, true, randomPrivateKey.GetPublicKey().EncodeCompressed());
            while (NetworkManagerBase.CycleNumber(_blockManager.GetHeight()) < 3)
            {
                var emptyBlock = BuildNextBlock();
                var exc = ExecuteBlock(emptyBlock);
                Assert.AreEqual(OperatingError.Ok, exc);
            }
            CheckBan(3, true, false, randomPrivateKey.GetPublicKey().EncodeCompressed());
            while (NetworkManagerBase.CycleNumber(_blockManager.GetHeight()) < 4)
            {
                var emptyBlock = BuildNextBlock();
                var exc = ExecuteBlock(emptyBlock);
                Assert.AreEqual(OperatingError.Ok, exc);
            }
            CheckBan(4, false, false, new byte[0]);
        }

        private void VotePeer(byte[] peer)
        {
            var tries = 2;
            for (int iter = 0 ; iter < tries; iter++)
            {
                var tx = _banTracker.MakeBanRequestTransaction(NetworkManagerBase.MaxPenaltyTolerance + 1, peer);
                var error = _transactionPool.Add(tx);
                Assert.AreEqual(OperatingError.Ok, error);
            }
            var takenTxes = _transactionPool.Peek(1000, 1000, _blockManager.GetHeight() + 1);
            var block = BuildNextBlock(takenTxes.ToArray());
            var result = ExecuteBlock(block, takenTxes.ToArray());
            Assert.AreEqual(result, OperatingError.Ok);
        }

        private void CheckBan(ulong currentCycle, bool banned, bool voted, byte[] bannedPeer)
        {
            var bannedPeers = _banRepo.GetBannedPeers(currentCycle);
            var length = banned ? CryptoUtils.PublicKeyLength : 0;
            Assert.AreEqual(length, bannedPeers.Length);
            if (banned) Assert.AreEqual(bannedPeer, bannedPeers);
            var cycle = _banRepo.GetLowestCycle();
            Assert.AreEqual(currentCycle, cycle);
            cycle = _banRepo.GetLowestCycleForVote();
            Assert.AreEqual(currentCycle, cycle);
            var votedPeer = _banRepo.GetVotedPeers(currentCycle);
            if (voted)
            {
                Assert.AreEqual(bannedPeers, votedPeer);
                var voters = _banRepo.GetVotersForBannedPeer(currentCycle, votedPeer);
                Assert.AreEqual(CryptoUtils.PublicKeyLength, voters.Length);
                Assert.AreEqual(_wallet.EcdsaKeyPair.PublicKey, voters.ToPublicKey());
            }
        }

        private Block BuildNextBlock(TransactionReceipt[]? receipts = null)
        {
            receipts ??= new TransactionReceipt[] { };

            var merkleRoot = UInt256Utils.Zero;

            if (receipts.Any())
                merkleRoot = MerkleTree.ComputeRoot(receipts.Select(tx => tx.Hash).ToArray()) ??
                             throw new InvalidOperationException();

            var predecessor =
                _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(_stateManager.LastApprovedSnapshot.Blocks
                    .GetTotalBlockHeight());
            var (header, multisig) =
                BuildHeaderAndMultisig(merkleRoot, predecessor, _stateManager.LastApprovedSnapshot.StateHash);

            return new Block
            {
                Header = header,
                Hash = header.Keccak(),
                Multisig = multisig,
                TransactionHashes = {receipts.Select(tx => tx.Hash)},
            };
        }

        private (BlockHeader, MultiSig) BuildHeaderAndMultisig(UInt256 merkleRoot, Block? predecessor,
            UInt256 stateHash)
        {
            var blockIndex = predecessor!.Header.Index + 1;
            var header = new BlockHeader
            {
                Index = blockIndex,
                PrevBlockHash = predecessor!.Hash,
                MerkleRoot = merkleRoot,
                StateHash = stateHash,
                Nonce = blockIndex
            };

            var keyPair = _wallet.EcdsaKeyPair;

            var headerSignature = Crypto.SignHashed(
                header.Keccak().ToBytes(),
                keyPair.PrivateKey.Encode(), HardforkHeights.IsHardfork_9Active(blockIndex)
            ).ToSignature(HardforkHeights.IsHardfork_9Active(blockIndex));

            var multisig = new MultiSig
            {
                Quorum = 1,
                Validators = {_wallet.EcdsaKeyPair.PublicKey},
                Signatures =
                {
                    new MultiSig.Types.SignatureByValidator
                    {
                        Key = _wallet.EcdsaKeyPair.PublicKey,
                        Value = headerSignature,
                    }
                }
            };
            return (header, multisig);
        }

        private OperatingError ExecuteBlock(Block block, TransactionReceipt[]? receipts = null)
        {
            _networkManager.AdvanceEra(_blockManager.GetHeight() + 1);
            receipts ??= new TransactionReceipt[] { };

            var (_, _, stateHash, _) = _blockManager.Emulate(block, receipts);

            var predecessor =
                _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(_stateManager.LastApprovedSnapshot.Blocks
                    .GetTotalBlockHeight());
            var (header, multisig) = BuildHeaderAndMultisig(block.Header.MerkleRoot, predecessor, stateHash);

            block.Header = header;
            block.Multisig = multisig;
            block.Hash = header.Keccak();

            var status = _blockManager.Execute(block, receipts, true, true);
            Console.WriteLine($"Executed block: {block.Header.Index}");
            return status;
        }
    }
}