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
using Lachain.Core.ValidatorStatus;
using Lachain.Crypto.ECDSA;
using System.Security.Cryptography;
using Lachain.Core.Blockchain.VM;
using Lachain.Utility.Serialization;
using Lachain.Core.Blockchain.SystemContracts.Storage;
using Lachain.Core.Consensus;

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
        private IValidatorStatusManager _validatorStatusManager = null!;
        private IConsensusManager _consensusManager = null!;

        private BlockchainServiceWeb3 _apiService = null!;

        private IConfigManager _configManager = null!;        

        [SetUp]
        public void Setup()
        {

            _container?.Dispose();
            TestUtils.DeleteTestChainData();

            var containerBuilder = new SimpleInjectorContainerBuilder(new ConfigManager(
                Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config2.json"),
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
            _validatorStatusManager = _container.Resolve<IValidatorStatusManager>();
            _configManager = _container.Resolve<IConfigManager>();
            _consensusManager = _container.Resolve<IConsensusManager>();



            ServiceBinder.BindService<GenericParameterAttributes>();
            _apiService = new BlockchainServiceWeb3(_transactionManager, _blockManager, _transactionPool,
                _stateManager, _snapshotIndexer, _networkManager, _nodeRetrieval, _systemContractReader, _consensusManager);
            


        }

        [TearDown]
        public void Teardown()
        {
            _validatorStatusManager.Stop();
            
            _container?.Dispose();
            TestUtils.DeleteTestChainData();

            var sessionId = Handler.GetSessionHandler().SessionId;
            if(sessionId != null) Handler.DestroySession(sessionId);

        }

        
        [Test]
        [Repeat(2)]
        // changed from private to public: GetBlockByNumber() , GetBlockRawByNumber(), GetBlockByHash()
        public void Test_Web3Block() 
        {
            
            ulong total = 1;
            GenerateBlocksWithGenesis(total);
            bool fullTx = false;
            var tx = GiveMeSomeMoney(Money.Parse("100000").ToUInt256());
            var txList = new List<TransactionReceipt> ();
            txList.Add(tx);
            AddBatchTransactionToPool(txList, false);
            total++;
            GenerateBlocks(1,total);
            CheckBlockWeb3Format("latest",(ulong)total , fullTx);
            var randomTxList = GetRandomTransactionBatch(10);
            var topUpTxList = TopUpBalanceTxBatch(randomTxList, "10");
            AddBatchTransactionToPool(topUpTxList, false);
            total++;
            GenerateBlocks(1,total);
            CheckBlockWeb3Format("latest",(ulong)total , fullTx);
            AddBatchTransactionToPool(randomTxList, false);
            total++;
            GenerateBlocks(1,total);
            CheckBlockWeb3Format("latest",(ulong)total , fullTx);
            CheckBlockWeb3Format( "earliest", 0, fullTx);
            CheckBlockWeb3Format( "pending", (ulong)total + 1, fullTx);
            ulong someBlockNumber = 100;
            CheckBlockWeb3Format( someBlockNumber.ToHex(), someBlockNumber, fullTx);
        }

        [Test]
        [Repeat(2)]
        // changed from private to public: GetBlockRawByNumberBatch()
        public void Test_Web3BlockBatch() 
        {
            ulong total = 5;
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
                CheckHex(item.ToString());
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
        // changed from private to public: GetStateByNumber() , GetAllTrieRootsHash() , GetRootHashByTrieName() , GetRootVersionByTrieName()
        public void Test_Web3State() 
        {
            
            
            ulong total = 5;
            GenerateBlocksWithGenesis(total);
            string blockTag = total.ToHex();
            var state = _apiService.GetStateByNumber(blockTag);
            CheckStateWeb3Format(state! , blockTag);

            blockTag = "latest";
            var sameState = _apiService.GetStateByNumber(blockTag);
            Assert.AreEqual(sameState, state);
            CheckStateWeb3Format(sameState!, blockTag);

            blockTag = "0x0";
            state = _apiService.GetStateByNumber(blockTag);
            CheckStateWeb3Format(state!, blockTag);

            blockTag = "earliest";
            sameState = _apiService.GetStateByNumber(blockTag);
            Assert.AreEqual(sameState, state);
            CheckStateWeb3Format(sameState!, blockTag);

            ulong someBlockNo = 2;
            blockTag = someBlockNo.ToHex();
            state = _apiService.GetStateByNumber(blockTag);
            CheckStateWeb3Format(state!, blockTag);

            someBlockNo = 100;
            blockTag = someBlockNo.ToHex();
            blockTag = "pending";
            state = _apiService.GetStateByNumber(blockTag);
            CheckStateWeb3Format(state!, blockTag);
        }

        [Test]
        [Repeat(2)]
        // changed from private to public: CheckNodeHashes()
        public void Test_CheckNodeHashes()
        {

            ulong total = 5;
            GenerateBlocksWithGenesis(total);
            for(ulong iter = 0; iter <= total; iter++)
            {
                Assert.AreEqual(Convert.ToUInt64(true).ToHex(false), _apiService.CheckNodeHashes(iter.ToHex()));
            }
            
        }

        [Test]
        [Repeat(2)]
        // changed from private to public: GetStateHashFromTrieRootsRange() , GetStateHashFromTrieRoots()
        public void Test_Web3StateHashFromTrieRoots() 
        {
            _blockManager.TryBuildGenesisBlock();
            ulong total = 5;
            GenerateBlocks(total,1);
            ulong startBlock = 0, endBlock = total;
            var stateHash = _apiService.GetStateHashFromTrieRootsRange(startBlock.ToHex(), endBlock.ToHex());
            Assert.AreEqual(stateHash![startBlock.ToHex(false)] , stateHash[endBlock.ToHex(false)]);
            List<JToken> stateHashList1 = new List<JToken>() , stateHashList2 = new List<JToken>();
            foreach(var (_,value) in stateHash)
            {
                stateHashList1.Add(value!);
            }
            for(var currentBlock = startBlock; currentBlock <= endBlock; currentBlock++)
            {
                stateHashList2.Add(_apiService.GetStateHashFromTrieRoots(currentBlock.ToHex()));
            }
            Assert.AreEqual(stateHashList1, stateHashList2);
        }

        [Test]
        [Repeat(2)]
        // changed from private to public: GetNodeByVersion() , GetNodeByHash(), GetChildrenByVersion() , GetChildrenByHash()
        public void Test_Web3Node() 
        {
            
            ulong total = 5;
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
                    var sameHash = web3NodeByHash["Hash"]!.ToString();
                    CheckHex(hash!);
                    Assert.AreEqual(hash, sameHash);
                    Assert.AreEqual(hash, node.Hash.ToHex());
                    if(web3Node["NodeType"]?.ToString() == "0x2")
                    {
                        CheckHex(web3Node["KeyHash"]?.ToString()!);
                        CheckHex(web3Node["Value"]?.ToString()!);
                    }

                    
                   

                    if (web3Node["NodeType"]!.ToString() == ((int)NodeType.Internal).ToHex(false)) // internal node: must have children
                    {
                        
                        var children = _apiService.GetChildrenByVersion(nodeId.ToHex());
                        var sameChildren = _apiService.GetChildrenByHash(node.Hash.ToHex())[node.Hash.ToHex()];
                        
                        var childrenArray = children[nodeId.ToHex(false)];

                        List<string> childrenHash = new List<string>(), sameChildrenHash = new List<string>(), anotherSameChildrenHash = new List<string>();
                        foreach (var item in childrenArray!)
                        {
                            var childHash = item["Hash"]!.ToString();
                            CheckHex(childHash);
                            childrenHash.Add(childHash);
                        }

                        var childrenFromNode = web3NodeByHash["ChildrenHash"];
                        foreach (var item in childrenFromNode!)
                        {
                            sameChildrenHash.Add(item.ToString());
                        }

                        foreach (var item in sameChildren!)
                        {
                            anotherSameChildrenHash.Add(item["Hash"]!.ToString());
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
        [Repeat(2)]
        // changed from private to public: GetTransactionsByBlockHash(), GetBlockTransactionsCountByNumber(), GetBlockTransactionsCountByHash()
        public void Test_Web3Transactions() 
        {

            GenerateBlocksWithGenesis(1);

            //var customReceipts = GetCustomTransactionBatch(10, "0.00000000001", "0.000000000000000001");
            var customReceipts = GetCustomTransactionBatch(10,"0.00000000001","0.000000000000000001");
            // set coverTxFee = "10" if GetRandomTransactionBatch() is used instead of GetCustomTransactionBatch()
            var coverTxFee = "0.0001"; 
            var topUpReceipts = TopUpBalanceTxBatch(customReceipts, coverTxFee); 
            Logger.LogInformation("Adding topUpReceipts transactions in Pool:");
            foreach (var tx in topUpReceipts)
            {
                Logger.LogInformation($"{tx.Hash.ToHex()}");
            }
            AddBatchTransactionToPool(topUpReceipts,false);
            ulong total = 2;
            GenerateBlocks(1,total);
            
            Logger.LogInformation("Adding randomReceipts transaction in Pool:");
            foreach (var tx in customReceipts)
            {
                Logger.LogInformation($"{tx.Hash.ToHex()}");
            }
            
            AddBatchTransactionToPool(customReceipts,false);
            total++;
            GenerateBlocks(1,total);
            for (ulong blockId = 0; blockId <= total*2 + 5; blockId++)
            {
                CheckTransactionWeb3Format(blockId);
            }
        }



        [Test]
        [Repeat(2)]
        // changed from private to public: GetBlockNumber(), GetDownloadedNodesTillNow(), ChainId(), NetVersion()
        public void Test_Web3Format() 
        {
            ulong total = 5;
            GenerateBlocksWithGenesis(total);
            var noOfBlocks = _apiService.GetBlockNumber();
            CheckHex(noOfBlocks);
            Assert.AreEqual(total.ToHex(false), noOfBlocks);
            CheckHex(_apiService.ChainId());
            CheckHex(_apiService.NetVersion());
            // does not work if number of downloaded nodes is 0;
            //var nodes = _apiService.GetDownloadedNodesTillNow();
            //CheckHex(nodes);
        }

        [Test]
        [Repeat(2)]
        // changed from private to public: GetValidatorInfo()
        public void Test_ValidatorInfo() 
        {
            GenerateBlocksWithGenesis(1);
            Init();
        }

        [Test]
        [Repeat(2)]
        // changed from private to public: GetEventsByTransactionHash()
        public void Test_Wev3Events()
        {
            GenerateBlocksWithGenesis(0);
            var randomtx = TestUtils.GetRandomTransaction();
            var coverTxFee = Money.Parse("10");
            var topUpTx = TopUpBalanceTx(randomtx.Transaction.From, (randomtx.Transaction.Value.ToMoney() + coverTxFee).ToUInt256(), 0);
            Console.WriteLine($"Sending tx: {topUpTx.Hash.ToHex()} and {randomtx.Hash.ToHex()}");
            _transactionPool.Add(topUpTx , false);
            ulong total = 1;
            GenerateBlocks(1,total);
            _transactionPool.Add(randomtx, false);
            total++;
            GenerateBlocks(1,total);
            
            CheckEvents(_apiService.GetEventsByTransactionHash(topUpTx.Hash.ToHex()));
            CheckEvents(_apiService.GetEventsByTransactionHash(topUpTx.Hash.ToHex()));
        }

        [Test]
        [Repeat(2)]
        // changed from private to public: GetTransactionPool(), GetTransactionPoolByHash()
        public void Test_TransactionPool()
        {
            GenerateBlocksWithGenesis(0);
            CheckTransactionPoolWeb3();
            var tx = GiveMeSomeMoney(Money.Parse("10").ToUInt256());
            var txList = new List<TransactionReceipt> ();
            txList.Add(tx);
            AddBatchTransactionToPool(txList, false);
            CheckTransactionPoolWeb3();
            ulong total = 1;
            GenerateBlocks(1,total);
            var randomTx = GetCustomTransactionBatch(5,"1","0.00000000001");
            var topUpTx = TopUpBalanceTxBatch(randomTx,"1");
            AddBatchTransactionToPool(topUpTx, false);
            CheckTransactionPoolWeb3();
            total++;
            GenerateBlocks(1,total);
            AddBatchTransactionToPool(randomTx,false);
            CheckTransactionPoolWeb3();
            total++;
            GenerateBlocks(1,total);
            Assert.AreEqual(0, _transactionPool.Size());
            CheckTransactionPoolWeb3();
        }

        [Test]
        [Repeat(2)]
        // changed from private to public: GetLogs()
        public void Test_GetLogs()
        {


            GenerateBlocksWithGenesis(0);
            var tx = GiveMeSomeMoney(Money.Parse("10").ToUInt256());
            var txList = new List<TransactionReceipt> ();
            txList.Add(tx);
            AddBatchTransactionToPool(txList, false);
            ulong total = 1;
            GenerateBlocks(1,total);
            var randomTx = GetCustomTransactionBatch(5 , "1" , "0.000000000001");
            var topUpTx = TopUpBalanceTxBatch(randomTx , "1");
            AddBatchTransactionToPool(topUpTx, false);
            total++;
            GenerateBlocks(1,total);
            AddBatchTransactionToPool(randomTx, false);
            total++;
            GenerateBlocks(1,total);

            var allTx = new List<TransactionReceipt>();
            allTx.AddRange(topUpTx);
            allTx.AddRange(randomTx);


            var input = new JObject();
            var defaultLogs = _apiService.GetLogs(input);

            input = new JObject();
            input["address"] = new JArray() { GetRandomAddress() };
            var emptyLogs = _apiService.GetLogs(input);
            Assert.AreEqual(0, emptyLogs.Count);
            var block = _blockManager.GetByHeight(10);
            CheckLogs(emptyLogs, block!, new List<TransactionReceipt>());

            input = new JObject();
            input["fromBlock"] = input["toBlock"] = "pending";
            var newEmptyLogs = _apiService.GetLogs(input);
            Assert.AreEqual(0, newEmptyLogs.Count);
            Assert.AreEqual(newEmptyLogs, emptyLogs);

            input = LogInputByBlockNo(0, 2);
            var allLogs = _apiService.GetLogs(input);

            input = new JObject();
            input["fromBlock"] = "earliest";
            input["toBlock"] = "latest";
            var logsForAllBlocks = _apiService.GetLogs(input);
            Assert.AreEqual(allLogs, logsForAllBlocks);



            input = LogInputByBlockNo(1, 1);
            var logsForBlock1 = _apiService.GetLogs(input);
            block = _blockManager.GetByHeight(1);
            CheckLogs(logsForBlock1, block!,topUpTx);

            input = LogInputByBlockNo(2, 2);
            var logsForBlock2 = _apiService.GetLogs(input);
            block = _blockManager.GetByHeight(2);
            CheckLogs(logsForBlock2, block!, randomTx);
            Assert.AreEqual(defaultLogs, logsForBlock2);
            if(logsForBlock1.Count > 0 ||  logsForBlock2.Count > 0)
                Assert.AreNotEqual(logsForBlock1, logsForBlock2);
            if(logsForBlock1.Count > 0 ||  allLogs.Count > 0)
                Assert.AreNotEqual(logsForBlock1, allLogs);
            if(allLogs.Count > 0 ||  logsForBlock2.Count > 0)
                Assert.AreNotEqual(logsForBlock2, allLogs);

        }



        [Test]
        [Repeat(2)]
        // changed from private to public: GetStorageAt()
        public void Test_StorageAt()
        {
            GenerateBlocksWithGenesis(1);
            ulong blockNumber = 1;
            var address = UInt160Utils.Zero;
            var randomKey = new Random().Next(1, 100).ToUInt256();
            var valueSet = SetRandomValueAtStorage(blockNumber.ToHex(), address, randomKey, true);
            CheckHex(valueSet.ToHex());
            Assert.AreNotEqual(UInt256Utils.Zero, valueSet);
            var blockchainSnapshot = _snapshotIndexer.GetSnapshotForBlock(blockNumber);
            var value = blockchainSnapshot.Storage.GetValue(address, randomKey);
            Assert.AreEqual(valueSet, value);
            var valueFromApi = _apiService.GetStorageAt(address.ToHex(), randomKey.ToHex(), blockNumber.ToHex());
            Assert.AreEqual(value.ToHex(),valueFromApi);
        }

        public UInt256 SetRandomValueAtStorage(string blockTag , UInt160 address , UInt256 key, bool save = false)
        {
            var blockNumber = GetBlockNumberByTag(blockTag);
            var blockchainSnapshot = _snapshotIndexer.GetSnapshotForBlock((ulong)blockNumber!);
            var randomValue = new Random().Next(1, 100).ToUInt256();
            blockchainSnapshot.Storage.SetValue(address, key, randomValue);
            if (save)
            {
                _snapshotIndexer.SaveSnapshotForBlock((ulong)blockNumber , blockchainSnapshot);
            }
            return randomValue;
        }


        public ulong? GetBlockNumberByTag(string blockTag)
        {
            return blockTag switch
            {
                "latest" => _blockManager.GetHeight(),
                "earliest" => 0,
                "pending" => null,
                _ => blockTag.HexToUlong()
            };
        }

        public void print(byte[] array)
        {
            Console.WriteLine($"array length: {array.Length} and array elements:");
            foreach(var item in array)
            {
                Console.WriteLine(item);
            }
        }

        public void CheckLogs(JArray logs, Block block, List<TransactionReceipt> txList)
        {
            foreach (var token in logs)
            {
                var blockHash = token["blockHash"]!.ToString();
                var txHash = token["transactionHash"]!.ToString();
                CheckHex(blockHash);
                CheckHex(txHash);
                Assert.AreEqual(block!.Hash.ToHex(), blockHash);
                Assert.That(txList.Any(tx => txHash == tx.Hash.ToHex()));
            }
        }

        public JObject LogInputByBlockNo(ulong fromBlock, ulong toBlock)
        {
            var JObj = new JObject();
            JObj["fromBlock"] = Web3DataFormatUtils.Web3Number(fromBlock);
            JObj["toBlock"] = Web3DataFormatUtils.Web3Number(toBlock);
            return JObj;
        }


        public JArray JArrayOfTxAddress(List < TransactionReceipt > txList)
        {
            var txAddress = new JArray();
            foreach (var tx in txList)
            {
                txAddress.Add(tx.Transaction.From.ToHex());
            }
            return txAddress;
        }

        public void CheckTransactionPoolWeb3()
        {
            var txHashes = _apiService.GetTransactionPool();
            Assert.AreEqual(_transactionPool.Size(), txHashes.Count);
            foreach(var txHash in txHashes)
            {
                var txHashInString = txHash.ToString();
                CheckHex(txHashInString);
                var txJObject = _apiService.GetTransactionPoolByHash(txHashInString);
                Assert.AreEqual(txHashInString, txJObject!["hash"]!.ToString());
            }
        }

        public List<TransactionReceipt> GetRandomTransactionBatch(int txCount)
        {
            
            var randomReceipts = new List<TransactionReceipt>();

            for (var i = 0; i < txCount; i++)
            {
                var tx = TestUtils.GetRandomTransaction();
                randomReceipts.Add(tx);
                
            }
            return randomReceipts;
        }

        public List<TransactionReceipt> GetCustomTransactionBatch(int txCount, string value, string gasPrice)
        {

            var customReceipts = new List<TransactionReceipt>();

            for (var i = 0; i < txCount; i++)
            {
                var tx = TestUtils.GetCustomTransaction(value , gasPrice);
                customReceipts.Add(tx);

            }
            return customReceipts;
        }

        public List<TransactionReceipt> TopUpBalanceTxBatch(List<TransactionReceipt> receipts , string coverTxFee = "100") 
        {

            var topUpReceipts = new List<TransactionReceipt>();
            if (receipts.Count == 0) return topUpReceipts;
            var txCount = receipts.Count;
            int nonce = 0;
            foreach (var receipt in receipts)
            {
                //var coverTxFee = receipt.Transaction.GasLimit * receipt.Transaction.GasPrice;
                var tx = TopUpBalanceTx(receipt.Transaction.From,(receipt.Transaction.Value.ToMoney() + Money.Parse(coverTxFee)).ToUInt256(),nonce);
                nonce++;
                topUpReceipts.Add(tx);
            }
            return topUpReceipts;
        }

        public void CheckEvents(JArray events)
        {
            foreach(var eachEvent in events)
            {
                CheckHex(eachEvent["transactionHash"]!.ToString());
                // block hash is null empty for unknown reason
                //CheckHex(eachEvent["blockHash"].ToString());
            }
        }


        public void CheckValidatorInfo(bool random)
        {
            string publicKey;
            if (random) publicKey = GetRandomPublicKey();
            else publicKey = _privateWallet.EcdsaKeyPair.PublicKey.ToHex();
            var validatorInfo = _apiService.GetValidatorInfo(publicKey);
            //Console.WriteLine(validatorInfo);
            if (random) Assert.AreNotEqual("Validator", validatorInfo["state"]!.ToString());
            else Assert.AreEqual("Validator", validatorInfo["state"]!.ToString());
        }

        public void Init()
        {
            CheckValidatorInfo(false);
            CheckValidatorInfo(true);
            UnlockWallet(true, 10000);
            var stake = "2000";
            _validatorStatusManager.StartWithStake(Money.Parse(stake).ToUInt256());
            CheckValidatorInfo(false);
            CheckValidatorInfo(true);
        }

        // returns random address in hex
        public string GetRandomAddress()
        {
            byte[] random = new byte[32];
            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            rng.GetBytes(random);
            var keyPair = new EcdsaKeyPair(random.ToPrivateKey());
            return keyPair.PublicKey.GetAddress().ToHex();
        }


        // returns random public key in hex
        public string GetRandomPublicKey()
        {
            byte[] random = new byte[32];
            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            rng.GetBytes(random);
            var keyPair = new EcdsaKeyPair(random.ToPrivateKey());
            return keyPair.PublicKey.ToHex();
        }

        public void AddBatchTransactionToPool(List<TransactionReceipt> txes , bool notify = true)
        {
            foreach(var tx in txes)
            {
                // TODO: If the second parameter is set to true, the test run is aborted for unknown reason
                var result = _transactionPool.Add(tx, notify); 
                Assert.AreEqual(OperatingError.Ok, result);
            }
        }

        private TransactionReceipt TopUpBalanceTx(UInt160 to, UInt256 value, int nonceInc)
        {
            var keyPair = new EcdsaKeyPair("0xd95d6db65f3e2223703c5d8e205d98e3e6b470f067b0f94f6c6bf73d4301ce48"
                .HexToBytes().ToPrivateKey());
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
            return Signer.Sign(tx, _privateWallet.EcdsaKeyPair, true);
        }

        private TransactionReceipt GiveMeSomeMoney(UInt256 money){
            var keyPair = new EcdsaKeyPair("0xd95d6db65f3e2223703c5d8e205d98e3e6b470f067b0f94f6c6bf73d4301ce48"
                .HexToBytes().ToPrivateKey());
            var tx = new Transaction
            {
                To = _privateWallet.EcdsaKeyPair.PublicKey.GetAddress(),
                From = keyPair.PublicKey.GetAddress(),
                GasPrice = (ulong)Money.Parse("0.000000000000000001").ToWei(),
                GasLimit = 4_000_000,
                Nonce = _transactionPool.GetNextNonceForAddress(keyPair.PublicKey.GetAddress()),
                Value = money
            };
            return Signer.Sign(tx, keyPair, true);
        } 

        public void CheckTransactionWeb3Format(ulong blockHeight)
        {
            var block = _blockManager.GetByHeight(blockHeight);
            var txCount = _apiService.GetBlockTransactionsCountByNumber(blockHeight.ToHex());

            if (block == null)
            {
                Console.WriteLine("null block");
                Assert.AreEqual(null, txCount);
                return;
            }
            CheckHex(txCount!);
            Assert.AreEqual(block.TransactionHashes.Count.ToHex(false), txCount);
            var sameTxCount = _apiService.GetBlockTransactionsCountByHash(block.Hash.ToHex());
            Assert.AreEqual(txCount, sameTxCount);
            var txes = _apiService.GetTransactionsByBlockHash(block.Hash.ToHex());
            var sameTxes = TxesForBlockInWeb3Format(block, true);
            var sameTxesAgain = _apiService.GetBlockByNumber(blockHeight.ToHex(),true);
            List<string> txesToList = new List<string>() , sameTxesToList = new List<string>(), sameTxesAgainToList = new List<string>();
            foreach(var item in txes!["transactions"]!)
            {
                txesToList.Add(item["hash"]!.ToString());
                CheckHex(item["hash"]!.ToString());
            }
            foreach(var item in sameTxes)
            {
                sameTxesToList.Add(item["hash"]!.ToString());
            }
            foreach(var item in sameTxesAgain!["transactions"]!)
            {
                sameTxesAgainToList.Add(item["hash"]!.ToString());
            }
            Assert.AreEqual(txesToList, sameTxesAgainToList);
            Assert.AreEqual(txesToList, sameTxesToList);
            
        }

        public void GenerateBlocksWithGenesis(ulong noOfBlocks)
        {
            _blockManager.TryBuildGenesisBlock();
            GenerateBlocks(noOfBlocks,1);
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
            foreach(var (_,value) in allTrieRootHash!)
            {
                Assert.AreEqual("0x", value!.ToString().Substring(0, 2));
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

                var rootVersion = _apiService.GetRootVersionByTrieName(name, blockTag);
                CheckHex(rootVersion);
            }
        }
        public void CheckBlockWeb3Format(string blockTag, ulong blockNumber , bool fullTx = false)
        {
            var web3Block = _apiService.GetBlockByNumber(blockTag, fullTx);
            var sameBlock = _apiService.GetBlockRawByNumber(blockTag);
            var block = _blockManager.GetByHeight(blockNumber);
            
            if (block is null)
            {
                Assert.AreEqual(null, web3Block);
                Assert.AreEqual(null, sameBlock);
                return;
            }
            Logger.LogInformation($"block hash: {block.Hash.ToHex()}");
            var web3BlockByHash = _apiService.GetBlockByHash(block.Hash.ToHex(),fullTx);
            
            Assert.AreEqual(block.Hash.ToHex(), web3Block!["hash"]!.ToString());
            CheckHex(sameBlock!);
            foreach(var item in web3Block)
            {
                var (key, pair) = item;
                if(key != "transactions" && key != "uncles")
                {
                    var pairToString = pair?.ToString();
                    
                    if (key != "extraData") CheckHex(pairToString!);
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
           
            var txs = new List<TransactionReceipt>();
            var txHashes = block!.TransactionHashes;
            foreach (var hash in txHashes)
            {
                txs.Add(_transactionManager.GetByHash(hash)!);
            }
            var txArray = fullTx? Web3DataFormatUtils.Web3BlockTransactionArray(txs, block!.Hash, block!.Header.Index) : new JArray();
            if (!fullTx)
            {
                foreach(var hash in txHashes)
                {
                    txArray.Add(Web3DataFormatUtils.Web3Data(hash));
                }
            }
            return txArray;

        }

        public ulong GasForBlock(Block block)
        {
            if (block is null) return 0;
            
            var txHashes = block!.TransactionHashes;
            ulong gasUsed = 0;
            foreach(var hash in txHashes)
            {
                Logger.LogInformation($"Transaction hash {hash.ToHex()} in block {block.Header.Index}");
                TransactionReceipt? tx = _transactionManager.GetByHash(hash);
                if(tx is null){
                    Logger.LogWarning($"Transaction not found in DB {hash.ToHex()}");
                }
                else gasUsed += tx.GasUsed;
            }
            
            return gasUsed;
        }

        
        private void GenerateBlocks(ulong blockNum, ulong era)
        {
            for (ulong i = 0; i < blockNum; i++)
            {
                lock (this)
                {
                    var txes = GetCurrentPoolTxes(era);
                    era++;
                    var block = BuildNextBlock(txes);
                    var result = ExecuteBlock(block, txes);
                    Assert.AreEqual(OperatingError.Ok, result);
                    
                }
                
            }
        }

        private TransactionReceipt[] GetCurrentPoolTxes(ulong era)
        {
            return _transactionPool.Peek(1000, 1000, era).ToArray();
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
                keyPair.PrivateKey.Encode(), true
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

            var (error, _, stateHash, _) = _blockManager.Emulate(block, receipts);
            Assert.AreEqual(OperatingError.Ok , error);
            //return OperatingError.Ok;
            var predecessor =
                _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(_stateManager.LastApprovedSnapshot.Blocks
                    .GetTotalBlockHeight());
            //return OperatingError.Ok;
            var (header, multisig) = BuildHeaderAndMultisig(block.Header.MerkleRoot, predecessor, stateHash);
            //return OperatingError.Ok;
            block.Header = header;
            block.Multisig = multisig;
            block.Hash = header.Keccak();
            //return OperatingError.Ok;
            var status = _blockManager.Execute(block, receipts, true, true);
            Assert.AreEqual(OperatingError.Ok , status);
            //return OperatingError.Ok;
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

        public void UnlockWallet(bool unlock, int time = 100){
            string password;
            if(unlock){
                var config = _configManager.GetConfig<VaultConfig>("vault") ??
                         throw new Exception("No 'vault' section in config file");
                password = config.Password!;
                Assert.AreEqual(true,_privateWallet.Unlock(password, time));
            }
            else{
                password = "12345" ; // random password
                Assert.AreEqual(false,_privateWallet.Unlock(password, time));
            }
        }
    }
}