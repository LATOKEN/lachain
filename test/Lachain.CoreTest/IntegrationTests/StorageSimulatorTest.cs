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
using Lachain.Core.Vault;
using Lachain.Crypto;
using Lachain.Crypto.ECDSA;
using Lachain.Crypto.Misc;
using Lachain.Logger;
using Lachain.Networking;
using Lachain.Proto;
using Lachain.Storage;
using Lachain.Storage.State;
using Lachain.Storage.Trie;
using Lachain.Utility;
using Lachain.Utility.Utils;
using Lachain.UtilityTest;
using NUnit.Framework;

namespace Lachain.CoreTest.IntegrationTests
{
    [TestFixture]
    public class StorageSimulatorTest
    {
        private static readonly ILogger<StorageSimulatorTest> Logger = LoggerFactory.GetLoggerForClass<StorageSimulatorTest>();
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();
        private static readonly ITransactionSigner Signer = new TransactionSigner();

        private IBlockManager _blockManager = null!;
        private ITransactionPool _transactionPool = null!;
        private IStateManager _stateManager = null!;
        private IStorageManager _storageManager = null!;
        private IPrivateWallet _wallet = null!;
        private IConfigManager _configManager = null!;
        private INodeRetrieval _nodeRetrieval = null!;
        private IContainer? _container;

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
            _storageManager = _container.Resolve<IStorageManager>();
            _wallet = _container.Resolve<IPrivateWallet>();
            _transactionPool = _container.Resolve<ITransactionPool>();
            _configManager = _container.Resolve<IConfigManager>();
            _nodeRetrieval = _container.Resolve<INodeRetrieval>();

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
        [Ignore("Doesn't work in mainnet")]
        public void Test()
        {
            // execute some blocks
            _blockManager.TryBuildGenesisBlock();
            var topUpReceipts = new List<TransactionReceipt>();
            var randomReceipts = new List<TransactionReceipt>();
            var txCount = 50;

            var coverTxFeeAmount = Money.Parse("10.0");
            for (var i = 0; i < txCount; i++)
            {
                var tx = TestUtils.GetRandomTransaction(HardforkHeights.IsHardfork_9Active(2));
                randomReceipts.Add(tx);
                topUpReceipts.Add(TopUpBalanceTx(tx.Transaction.From,
                    (tx.Transaction.Value.ToMoney() + coverTxFeeAmount).ToUInt256(), i, 
                    HardforkHeights.IsHardfork_9Active(1)));
            }

            ExecuteTxesBlocks(topUpReceipts);
            ExecuteTxesBlocks(randomReceipts);
            var addresses = new HashSet<UInt160>();
            foreach (var tx in topUpReceipts)
            {
                addresses.Add(tx.Transaction.From);
                addresses.Add(tx.Transaction.To);
            }
            foreach (var tx in randomReceipts)
            {
                addresses.Add(tx.Transaction.From);
                addresses.Add(tx.Transaction.To);
            }
            var addressBalance = new Dictionary<UInt160, Money>();
            foreach (var address in addresses)
            {
                var balance = _stateManager.LastApprovedSnapshot.Balances.GetBalance(address);
                Assert.That(addressBalance.TryAdd(address, balance));
            }
            var stateHash = _stateManager.LastApprovedSnapshot.StateHash;
            var snapshots = _stateManager.LastApprovedSnapshot.GetAllSnapshot();
            var nodeVersions = new Dictionary<uint, ulong>();
            var nodes = new Dictionary<uint, IHashTrieNode?>();
            foreach (var snapshot in snapshots)
            {
                var version = snapshot.Version;
                var node = _nodeRetrieval.TryGetNode(version);
                Assert.That(nodeVersions.TryAdd(snapshot.RepositoryId, version));
                Assert.That(nodes.TryAdd(snapshot.RepositoryId, node));
            }

            // run state simulator, it should not commit and should not change state
            // running state simulator should not affect _stateManager.LastApprovedSnapshot

            IStateManager stateSimulator = new StateSimulator(_storageManager);
            // it should have all latest states
            DiffSimulation(stateSimulator.LastApprovedSnapshot, true, stateHash, nodeVersions, nodes, addresses, addressBalance);

            // update balance of all of them
            stateSimulator.SafeContext(() =>
            {
                var newSnapshot = stateSimulator.NewSnapshot();
                SimulateState(newSnapshot, addresses, addressBalance);
                DiffSimulation(newSnapshot, false, stateHash, nodeVersions, nodes, addresses, addressBalance);
            });

            DiffSimulation(stateSimulator.LastApprovedSnapshot, true, stateHash, nodeVersions, nodes, addresses, addressBalance);
            var newSnapshot = stateSimulator.NewSnapshot();
            SimulateState(newSnapshot, addresses, addressBalance);
            stateSimulator.Approve();
            DiffSimulation(stateSimulator.LastApprovedSnapshot, false, stateHash, nodeVersions, nodes, addresses, addressBalance);
            stateSimulator = new StateSimulator(_storageManager);
            DiffSimulation(stateSimulator.LastApprovedSnapshot, true, stateHash, nodeVersions, nodes, addresses, addressBalance);
            DiffSimulation(_stateManager.LastApprovedSnapshot, true, stateHash, nodeVersions, nodes, addresses, addressBalance);
        }

        private void DiffSimulation(
            IBlockchainSnapshot currentSnapshot, bool match, UInt256 stateHash,
            Dictionary<uint, ulong> nodeVersions,
            Dictionary<uint, IHashTrieNode?> nodes,
            HashSet<UInt160> addresses,
            Dictionary<UInt160, Money> addressBalance
        )
        {
            var newStateHash = currentSnapshot.StateHash;
            Assert.AreEqual(match, stateHash.Equals(newStateHash));
            var snapshots = currentSnapshot.GetAllSnapshot();
            var matched = 0;
            var notMatched = 0;
            foreach (var snapshot in snapshots)
            {
                var version = snapshot.Version;
                var node = _nodeRetrieval.TryGetNode(version);
                Assert.That(nodeVersions.TryGetValue(snapshot.RepositoryId, out var realVersion));
                if (realVersion == version) matched++;
                else notMatched++;
                Assert.That(nodes.TryGetValue(snapshot.RepositoryId, out var realNode));
                if (match) Assert.AreEqual(realNode, node);
                else
                {
                    if (realVersion != version) Assert.AreNotEqual(realNode, node);
                    else Assert.AreEqual(realNode, node);
                }
            }
            if (match)
            {
                Assert.AreEqual(snapshots.Length, matched);
            }
            else Assert.That(notMatched > 0);

            foreach (var address in addresses)
            {
                var balance = currentSnapshot.Balances.GetBalance(address);
                Assert.That(addressBalance.TryGetValue(address, out var realBalance));
                Assert.AreEqual(match, balance.Equals(realBalance!));
            }
        }
        
        private void SimulateState(
            IBlockchainSnapshot snapshot, HashSet<UInt160>addresses, Dictionary<UInt160, Money> addressBalance
        )
        {
            var masterAddress = new EcdsaKeyPair("0xd95d6db65f3e2223703c5d8e205d98e3e6b470f067b0f94f6c6bf73d4301ce48"
                .HexToBytes().ToPrivateKey()).PublicKey.GetAddress();

            var balanceUpdate = Money.Parse("0.000000001");
            var oldBalance = snapshot.Balances.GetBalance(masterAddress);
            foreach (var address in addresses)
            {
                if (!address.Equals(masterAddress))
                {
                    Assert.That(snapshot.Balances.TransferBalance(
                        masterAddress, address, balanceUpdate, new TransactionReceipt(), true, true
                    ));
                    oldBalance -= balanceUpdate;
                }
            }

            foreach (var address in addresses)
            {
                var balance = snapshot.Balances.GetBalance(address);
                if (address.Equals(masterAddress))
                {
                    Assert.AreEqual(oldBalance, balance);
                }
                else
                {
                    Assert.That(addressBalance.TryGetValue(address, out var realBalance));
                    Assert.AreEqual(realBalance! + balanceUpdate, balance);
                }
            }
        }

        public void ExecuteTxesBlocks(List<TransactionReceipt> txes)
        {
            txes = txes.OrderBy(x => x, new ReceiptComparer()).ToList();
            foreach (var tx in txes)
            {
                _transactionPool.Add(tx);
            }
            var takenTxes = _transactionPool.Peek(txes.Count, txes.Count, _blockManager.GetHeight() + 1);
            var block = BuildNextBlock(takenTxes.ToArray());
            var result = ExecuteBlock(block, takenTxes.ToArray());
            Assert.AreEqual(result, OperatingError.Ok);
            var executedBlock = _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(block.Header.Index);
            Assert.AreEqual(executedBlock!.TransactionHashes.Count, takenTxes.Count);

            // check if the txes are executed properly
            foreach (var tx in takenTxes)
            {
                var executedTx = _stateManager.LastApprovedSnapshot.Transactions.GetTransactionByHash(tx.Hash);
                Assert.AreNotEqual(null, executedTx, $"Transaction {tx.Hash.ToHex()} not found");
                Assert.AreEqual(TransactionStatus.Executed, executedTx!.Status,
                    "Transaction {tx.Hash.ToHex()} was not executed properly");
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

        private TransactionReceipt TopUpBalanceTx(UInt160 to, UInt256 value, int nonceInc, bool useNewChainId)
        {
            var keyPair = new EcdsaKeyPair("0xd95d6db65f3e2223703c5d8e205d98e3e6b470f067b0f94f6c6bf73d4301ce48"
                .HexToBytes().ToPrivateKey());
            var tx = new Transaction
            {
                To = to,
                From = keyPair.PublicKey.GetAddress(),
                GasPrice = (ulong) Money.Parse("0.0000001").ToWei(),
                GasLimit = 4_000_000,
                Nonce = _transactionPool.GetNextNonceForAddress(keyPair.PublicKey.GetAddress()) +
                        (ulong) nonceInc,
                Value = value
            };
            return Signer.Sign(tx, keyPair, useNewChainId);
        }
    }
}