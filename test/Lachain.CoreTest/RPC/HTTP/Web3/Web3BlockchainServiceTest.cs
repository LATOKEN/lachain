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
using Lachain.Logger;
using Lachain.Utility;
using Lachain.Core.Blockchain.Operations;

namespace Lachain.CoreTest.RPC.HTTP.Web3
{
    public class Web3BlockchainServiceTest
    {
        private static readonly ILogger<Web3BlockchainServiceTest> Logger = LoggerFactory.GetLoggerForClass<Web3BlockchainServiceTest>();

        private static readonly ITransactionSigner Signer = new TransactionSigner();

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
        public void Test_Web3Block() // changed from private to public: GetBlockByNumber() , GetBlockRawByNumber(), GetBlockByHash()
        {
            
            ulong total = 50;
            GenerateBlocksWithGenesis(total);
            bool fullTx = true;
            CheckBlockWeb3Format("latest",(ulong)total , fullTx);
            CheckBlockWeb3Format( "earliest", 0, fullTx);
            CheckBlockWeb3Format( "pending", (ulong)total + 1, fullTx);
            ulong someBlockNumber = 100;
            CheckBlockWeb3Format( someBlockNumber.ToHex(), someBlockNumber, fullTx);
        }

        [Test]
        [Repeat(2)]
        public void Test_Web3BlockBatch() // changed from private to public: GetBlockRawByNumberBatch()
        {
            ulong total = 50;
            GenerateBlocksWithGenesis(total);
            var listOfBlockNo = new List<ulong>();
            var listOfBlockTag = new List<String>();
            for (ulong iter = 0; iter <= total+5; iter++)
            {
                listOfBlockNo.Add(iter);
                listOfBlockTag.Add(iter.ToHex());
            }
            var rawBlockList = _apiService.GetBlockRawByNumberBatch(listOfBlockTag);
            foreach(var item in rawBlockList)
            {
                var str = item.ToString();
                // Assert.AreEqual("0x", string.Substring(0, 2)) to check if the string is valid
                // Assert.AreNotEqual("0x", string) to check if the string is not null
                Assert.AreEqual("0x", str.Substring(0, 2));
                Assert.AreNotEqual("0x", str);
            }
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

        [Test]
        [Repeat(2)]
        public void Test_Web3State() // changed from private to public: GetStateByNumber() , GetAllTrieRootsHash() , GetRootHashByTrieName()
        {
            
            
            ulong total = 50;
            GenerateBlocksWithGenesis(total);
            string blockTag = total.ToHex();
            var state = _apiService.GetStateByNumber(blockTag);
            CheckStateWeb3Format(state , blockTag);

            blockTag = "latest";
            var sameState = _apiService.GetStateByNumber(blockTag);
            Assert.AreEqual(sameState, state);

            blockTag = "0x0";
            state = _apiService.GetStateByNumber(blockTag);
            CheckStateWeb3Format(state, blockTag);

            blockTag = "earliest";
            sameState = _apiService.GetStateByNumber(blockTag);
            Assert.AreEqual(sameState, state);

            ulong someBlockNo = 20;
            blockTag = someBlockNo.ToHex();
            state = _apiService.GetStateByNumber(blockTag);
            CheckStateWeb3Format(state, blockTag);

            someBlockNo = 100;
            blockTag = someBlockNo.ToHex();
            blockTag = "pending";
            state = _apiService.GetStateByNumber(blockTag);
            CheckStateWeb3Format(state, blockTag);
        }

        [Test]
        [Repeat(2)]
        public void Test_CheckNodeHashes() // // changed from private to public: CheckNodeHashes()
        {
            
            ulong total = 50;
            GenerateBlocksWithGenesis(total);
            for(ulong iter = 0; iter <= total; iter++)
            {
                Assert.AreEqual(Convert.ToUInt64(true).ToHex(false), _apiService.CheckNodeHashes(iter.ToHex()));
            }
            
        }

        [Test]
        [Repeat(2)]
        public void Test_Web3StateHashFromTrieRoots() // changed from private to public: GetStateHashFromTrieRootsRange() , GetStateHashFromTrieRoots()
        {
            _blockManager.TryBuildGenesisBlock();
            ulong total = 50;
            GenerateBlocks(total);
            ulong startBlock = 0, endBlock = total;
            var stateHash = _apiService.GetStateHashFromTrieRootsRange(startBlock.ToHex(), endBlock.ToHex());
            Assert.AreEqual(stateHash[startBlock.ToHex(false)] , stateHash[endBlock.ToHex(false)]);
            List<JToken> stateHashList1 = new List<JToken>() , stateHashList2 = new List<JToken>();
            foreach(var (_,value) in stateHash)
            {
                stateHashList1.Add(value);
            }
            for(var currentBlock = startBlock; currentBlock <= endBlock; currentBlock++)
            {
                stateHashList2.Add(_apiService.GetStateHashFromTrieRoots(currentBlock.ToHex()));
            }
            Assert.AreEqual(stateHashList1, stateHashList2);
        }

        [Test]
        [Repeat(2)]
        public void Test_Web3Node() // changed from private to public: GetNodeByVersion() , GetNodeByHash(), GetChildrenByVersion() , GetChildrenByHash()
        {
            
            ulong total = 50;
            GenerateBlocksWithGenesis(total);
            int totalNodes = 0;
            ulong latestNode = 0;
            
            for(ulong nodeId = 0; nodeId <= 1000000; nodeId++)
            {
                var node = _nodeRetrieval.TryGetNode(nodeId);
                
                if (node != null)
                {
                    
                    var web3NodeByHash = _apiService.GetNodeByHash(node.Hash.ToHex());
                    var web3Node = _apiService.GetNodeByVersion(nodeId.ToHex());
                    
                    var hash = web3Node["Hash"]?.ToString();
                    var sameHash = web3NodeByHash["Hash"].ToString();
                    CheckHex(hash);
                    Assert.AreEqual(hash, sameHash);
                    Assert.AreEqual(hash, node.Hash.ToHex());
                    if(web3Node["NodeType"]?.ToString() == "0x2")
                    {
                        CheckHex(web3Node["KeyHash"]?.ToString());
                        CheckHex(web3Node["Value"]?.ToString());
                    }

                    
                   

                    if (web3Node["NodeType"].ToString() == ((int)NodeType.Internal).ToHex(false)) // internal node: must have children
                    {
                        
                        var children = _apiService.GetChildrenByVersion(nodeId.ToHex());
                        var sameChildren = _apiService.GetChildrenByHash(node.Hash.ToHex())[node.Hash.ToHex()];
                        
                        var childrenArray = children[nodeId.ToHex(false)];

                        List<string> childrenHash = new List<string>(), sameChildrenHash = new List<string>(), anotherSameChildrenHash = new List<string>();
                        foreach (var item in childrenArray)
                        {
                            var childHash = item["Hash"].ToString();
                            CheckHex(childHash);
                            childrenHash.Add(childHash);
                        }

                        var childrenFromNode = web3NodeByHash["ChildrenHash"];
                        foreach (var item in childrenFromNode)
                        {
                            sameChildrenHash.Add(item.ToString());
                        }

                        foreach (var item in sameChildren)
                        {
                            anotherSameChildrenHash.Add(item["Hash"].ToString());
                        }

                        Assert.AreEqual(childrenHash, sameChildrenHash);
                        Assert.AreEqual(childrenHash, anotherSameChildrenHash);
                    }

                    totalNodes++;
                    latestNode = nodeId;
                }
            }
            Console.WriteLine($"total nodes: {totalNodes} , last node ID: {latestNode}");
        }

        [Test]
        public void Test_A()
        {
            var x = (int)NodeType.Internal;
            Console.WriteLine(x.ToHex(false));
        }

        //[Test]
        //[Repeat(1)]
        //public void Test_Web3TransactionsByBlockHash() // changed from private to public: GetTransactionsByBlockHash()
        //{
            
        //    GenerateBlocksWithGenesis(1);
        //    var txCheck = TestUtils.GetRandomTransaction();
        //    _transactionPool.Add(txCheck);
        //    var topUpReceipts = new List<TransactionReceipt>();
        //    var randomReceipts = new List<TransactionReceipt>();
        //    var txCount = 10;
          
        //    var coverTxFeeAmount = Money.Parse("0.0001");
        //    for (var i = 0; i < txCount; i++)
        //    {
        //        var tx = TestUtils.GetCustomTransaction("1.0","0.000000000000000001");
        //        randomReceipts.Add(tx);
        //        topUpReceipts.Add(TopUpBalanceTx(tx.Transaction.From,
        //            (tx.Transaction.Value.ToMoney() + coverTxFeeAmount).ToUInt256(), i));
        //    }
        //    AddBatchTransactionToPool(topUpReceipts);
        //    ulong total = 1;
        //    GenerateBlocks(total);
        //    for (ulong blockId = 0; blockId <= total; blockId++)
        //    {
        //        CheckTransactionWeb3Format(blockId);
        //    }
        //    //AddBatchTransactionToPool(randomReceipts);
        //    //GenerateBlocks(total);
        //    //for (ulong blockId = 0; blockId <= total * 2; blockId++)
        //    //{
        //    //    CheckTransactionWeb3Format(blockId);
        //    //}
        //}

        public void AddBatchTransactionToPool(List<TransactionReceipt> txes)
        {
            foreach(var tx in txes)
            {
                var result = _transactionPool.Add(tx);
                Assert.AreEqual(OperatingError.Ok, result);
            }
        }

        private TransactionReceipt TopUpBalanceTx(UInt160 to, UInt256 value, int nonceInc)
        {
            var tx = new Transaction
            {
                To = to,
                From = _privateWallet.EcdsaKeyPair.PublicKey.GetAddress(),
                GasPrice = (ulong)Money.Parse("0.000000000000000001").ToWei(),
                GasLimit = 4_000_000,
                Nonce = _transactionPool.GetNextNonceForAddress(_privateWallet.EcdsaKeyPair.PublicKey.GetAddress()) +
                        (ulong)nonceInc,
                Value = value
            };
            return Signer.Sign(tx, _privateWallet.EcdsaKeyPair);
        }

        public void CheckTransactionWeb3Format(ulong blockHeight)
        {
            var block = _blockManager.GetByHeight(blockHeight);
            var txes = _apiService.GetTransactionsByBlockHash(block.Hash.ToHex());
            var sameTxes = TxesForBlockInWeb3Format(block, true);
            var sameTxesAgain = _apiService.GetBlockByNumber(blockHeight.ToHex(),true);
            Console.WriteLine(txes);
            Console.WriteLine(sameTxes);
            Console.WriteLine(sameTxesAgain);
            //if(blockHeight > 0) Assert.AreEqual(txes["transactions"], sameTxes);
        }

        public void GenerateBlocksWithGenesis(ulong noOfBlocks)
        {
            _blockManager.TryBuildGenesisBlock();
            GenerateBlocks(noOfBlocks);
        }

        public void CheckStateWeb3Format(JObject state , string blockTag)
        {
            if(state == null)
            {
                Logger.LogDebug("Null State!");
                return;
            }
            foreach (var (key, token) in state)
            {
                if(key.Length > 4 && key.Substring(key.Length - 4,4) == "Root")
                {
                    Assert.AreEqual("0x", token?.ToString().Substring(0,2));
                    Assert.AreNotEqual("0x", token?.ToString());
                }
            }

            var allTrieRootHash = _apiService.GetAllTrieRootsHash(blockTag);
            foreach(var (_,value) in allTrieRootHash)
            {
                Assert.AreEqual("0x", value.ToString().Substring(0, 2));
                Assert.AreNotEqual("0x", value.ToString());
            }
            string[] trieNames = new string[] { "Balances", "Contracts", "Storage", "Transactions", "Blocks", "Events", "Validators" };
            string prefix = "Lachain.Storage.State.", suffix = "SnapshotHash";
            foreach(var name in trieNames)
            {
                var rootHash = _apiService.GetRootHashByTrieName(name, blockTag);
                string key = name;
                if (name != "Storage") key = key.Remove(key.Length - 1);
                key = prefix + key + suffix;
                Assert.AreEqual(allTrieRootHash[key]?.ToString() , rootHash);
            }
        }
        public void CheckBlockWeb3Format(string blockTag, ulong blockNumber , bool fullTx = false)
        {
            var web3Block = _apiService.GetBlockByNumber(blockTag, fullTx);
            var sameBlock = _apiService.GetBlockRawByNumber(blockTag);
            var block = _blockManager.GetByHeight(blockNumber);
            
            if (block == null!)
            {
                Assert.AreEqual(null!, web3Block);
                Assert.AreEqual(null!, sameBlock);
                return;
            }

            var web3BlockByHash = _apiService.GetBlockByHash(block.Hash.ToHex());
            
            Assert.AreEqual(block.Hash.ToHex(), web3Block["hash"].ToString());
            CheckHex(sameBlock);
            foreach(var item in web3Block)
            {
                var (key, pair) = item;
                if(key != "transactions" && key != "uncles")
                {
                    var pairToString = pair?.ToString();
                    
                    if (key != "extraData") CheckHex(pairToString);
                    else Assert.AreEqual("0x", pairToString?.Substring(0, 2));
                }
            }
            var gasUsed = GasForBlock(block);
            var txArray = TxesForBlockInWeb3Format(block, fullTx);
            Assert.AreEqual(web3Block, Web3DataFormatUtils.Web3Block(block!, gasUsed, txArray));
            Assert.AreEqual(sameBlock, Web3DataFormatUtils.Web3BlockRaw(block));
            Assert.AreEqual(web3BlockByHash, web3Block);
        }

        public JArray TxesForBlockInWeb3Format(Block block , bool fullTx)
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
                foreach(var item in txes)
                {
                    Console.WriteLine($"tx: {item}");
                }
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

        public void CheckHex(string hex)
        {
            // Assert.AreEqual("0x", string.Substring(0, 2)) to check if the string is valid
            // Assert.AreNotEqual("0x", string) to check if the string is not null
            Assert.AreEqual("0x", hex?.Substring(0, 2));
            Assert.AreNotEqual("0x", hex);
        }

    }
}