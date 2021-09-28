using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.CLI;
using Lachain.Core.Config;
using Lachain.Core.DI;
using Lachain.Core.DI.Modules;
using Lachain.Core.DI.SimpleInjector;
using Lachain.Core.RPC.HTTP.Web3;
using Lachain.Core.Vault;
using Lachain.Crypto;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Utility.Utils;
using Lachain.UtilityTest;
using NUnit.Framework;
using AustinHarris.JsonRpc;
using Lachain.Storage.Repositories;
using Lachain.Storage.Trie;
using Lachain.Networking;
using Lachain.Core.Blockchain.Error;
using Lachain.Crypto.Misc;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Lachain.CoreTest.RPC.HTTP.Web3
{
    public class Web3BlockchainServiceTest
    {
        private IContainer? _container;
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();
        private IStateManager _stateManager = null!;
        private IBlockManager _blockManager = null!;
        private ISnapshotIndexRepository _snapshotIndexer = null!;
        private INetworkManager _networkManager = null!;
        private INodeRetrieval _nodeRetrieval = null!;
        private ISystemContractReader _systemContractReader = null!;
        private ITransactionManager _transactionManager = null!;
        private ITransactionBuilder _transactionBuilder = null!;
        private ITransactionSigner _transactionSigner = null!;
        private ITransactionPool _transactionPool = null!;
        private IContractRegisterer _contractRegisterer = null!;
        private IPrivateWallet _privateWallet = null!;

        private BlockchainServiceWeb3 _apiService = null!;

        [SetUp]
        public void Setup()
        {

            _container?.Dispose();
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

            _stateManager = _container.Resolve<IStateManager>();
            _transactionManager = _container.Resolve<ITransactionManager>();
            _transactionBuilder = _container.Resolve<ITransactionBuilder>();
            _transactionSigner = _container.Resolve<ITransactionSigner>();
            _transactionPool = _container.Resolve<ITransactionPool>();
            _contractRegisterer = _container.Resolve<IContractRegisterer>();
            _privateWallet = _container.Resolve<IPrivateWallet>();
            _snapshotIndexer = _container.Resolve<ISnapshotIndexRepository>();
            _networkManager = _container.Resolve<INetworkManager>();
            _nodeRetrieval = _container.Resolve<INodeRetrieval>();
            _systemContractReader = _container.Resolve<ISystemContractReader>();
            _blockManager = _container.Resolve<IBlockManager>();

            ServiceBinder.BindService<GenericParameterAttributes>();
            _apiService = new BlockchainServiceWeb3(_transactionManager, _blockManager, _transactionPool,
                _stateManager, _snapshotIndexer, _networkManager, _nodeRetrieval, _systemContractReader);


        }

        [TearDown]
        public void Teardown()
        {
            var sessionId = Handler.DefaultSessionId();
            Handler.DestroySession(sessionId);
            _container?.Dispose();
            TestUtils.DeleteTestChainData();

        }


        [Test]
        [Repeat(2)]
        public void Test_GetBlockByNumber() // changed from private to public: GetBlockByNumber() , GetBlockRawByNumber()
        {
            _blockManager.TryBuildGenesisBlock();
            ulong total = 50;
            GenerateBlocks(total);
            bool fullTx = true;
            CheckBlockWeb3Format("latest",(ulong)total , fullTx);
            CheckBlockWeb3Format( "earliest", 0, fullTx);
            CheckBlockWeb3Format( "pending", (ulong)total + 1, fullTx);
            ulong someBlockNumber = 100;
            CheckBlockWeb3Format( someBlockNumber.ToHex().ToString(), someBlockNumber, fullTx);
        }

        [Test]
        [Repeat(2)]
        public void Test_GetBlockRawByNumberBatch() // changed from private to public: GetBlockRawByNumberBatch()
        {
            _blockManager.TryBuildGenesisBlock();
            ulong total = 50;
            GenerateBlocks(total);
            var listOfBlockNo = new List<ulong>();
            var listOfBlockTag = new List<String>();
            for (ulong iter = 0; iter <= total+5; iter++)
            {
                listOfBlockNo.Add(iter);
                listOfBlockTag.Add(iter.ToHex().ToString());
            }
            var rawBlockList = _apiService.GetBlockRawByNumberBatch(listOfBlockTag);
            var BlockToJArray = new JArray();
            foreach(var item in listOfBlockNo)
            {
                var block = _blockManager.GetByHeight(item);
                if(block == null)
                {
                    continue;
                }
                BlockToJArray.Add(Web3DataFormatUtils.Web3BlockRaw(block!));
            }
            
            Assert.AreEqual(BlockToJArray, rawBlockList);
        }

        public void CheckBlockWeb3Format(string blockTag, ulong blockNumber , bool fullTx = false)
        {
            var block = _apiService.GetBlockByNumber(blockTag, fullTx);
            
            var sameBlock = _apiService.GetBlockRawByNumber(blockTag);
            var latestBlock = _blockManager.GetByHeight(blockNumber);
            if (latestBlock == null!)
            {
                Assert.AreEqual(null!, block);
                Assert.AreEqual(null!, sameBlock);
                return;
            }
            // Assert.AreEqual("0x", string.Substring(0, 2)) to check if the string is valid
            // Assert.AreNotEqual("0x", string) to check if the string is not null
            Assert.AreEqual("0x", block["hash"].ToString().Substring(0, 2));
            Assert.AreEqual("0x", sameBlock.Substring(0, 2)) ;
            Assert.AreNotEqual("0x", block["hash"].ToString());
            Assert.AreNotEqual("0x", sameBlock);
            foreach(var item in block)
            {
                var (key, pair) = item;
                if(key != "transactions" && key != "uncles")
                {
                    var str = pair?.ToString();
                    Assert.AreEqual("0x", str?.Substring(0, 2));
                    if(key != "extraData") Assert.AreNotEqual("0x", str);
                }
            }
            var gasUsed = GasForBlock(latestBlock);
            var txArray = TxsForBlockInWeb3Format(latestBlock, fullTx);
            Assert.AreEqual(block, Web3DataFormatUtils.Web3Block(latestBlock!, gasUsed, txArray));
            Assert.AreEqual(sameBlock, Web3DataFormatUtils.Web3BlockRaw(latestBlock));
        }

        public JArray TxsForBlockInWeb3Format(Block block , bool fullTx)
        {
            if (!fullTx) return new JArray();
            var txs = new List<TransactionReceipt>();
            var txHashes = block!.TransactionHashes;
            foreach (var hash in txHashes)
            {
                txs.Add(_transactionManager.GetByHash(hash)!);
            }
            return Web3DataFormatUtils.Web3BlockTransactionArray(txs, block!.Hash, block!.Header.Index);
        }

        public ulong GasForBlock(Block block)
        {
            if (block == null!) return 0;
            
            var txs = new List<TransactionReceipt>();
            var txHashes = block!.TransactionHashes;
            foreach(var hash in txHashes)
            {
                txs.Add(_transactionManager.GetByHash(hash)!);
            }
            
            ulong gasUsed = 0;
            foreach(var tx in txs)
            {
                gasUsed += tx.GasUsed;
            }
            return gasUsed;
        }

        private void GenerateBlocks(ulong blockNum)
        {
            for (ulong i = 0; i < blockNum; i++)
            {
                var txes = GetCurrentPoolTxes();
                var block = BuildNextBlock(txes);
                var result = ExecuteBlock(block, txes);
                Assert.AreEqual(OperatingError.Ok, result);
            }
        }

        private TransactionReceipt[] GetCurrentPoolTxes()
        {
            return _transactionPool.Peek(1000, 1000).ToArray();
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
                TransactionHashes = { receipts.Select(tx => tx.Hash) },
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

            var keyPair = _privateWallet.EcdsaKeyPair;

            var headerSignature = Crypto.SignHashed(
                header.Keccak().ToBytes(),
                keyPair.PrivateKey.Encode()
            ).ToSignature();

            var multisig = new MultiSig
            {
                Quorum = 1,
                Validators = { _privateWallet.EcdsaKeyPair.PublicKey },
                Signatures =
                {
                    new MultiSig.Types.SignatureByValidator
                    {
                        Key = _privateWallet.EcdsaKeyPair.PublicKey,
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

    }
}
