using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Collections.Generic;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Operations;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.VM;
using Lachain.Core.CLI;
using Lachain.Core.Config;
using Lachain.Core.DI;
using Lachain.Core.DI.Modules;
using Lachain.Core.DI.SimpleInjector;
using Lachain.Core.Vault;
using Lachain.Crypto;
using Lachain.Crypto.Misc;
using Lachain.Networking;
using Lachain.Proto;
using Lachain.Storage;
using Lachain.Storage.Trie;
using Lachain.Storage.State;
using Lachain.Storage.Repositories;
using Lachain.Utility;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using Lachain.UtilityTest;
using NUnit.Framework;
using Lachain.Logger;
using Google.Protobuf;

namespace Lachain.CoreTest.IntegrationTests
{
    [TestFixture]
    public class NetworkingTest
    {
        private static readonly ILogger<NetworkingTest> Logger = LoggerFactory.GetLoggerForClass<NetworkingTest>();
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();
        private IBlockManager _blockManager = null!;
        private ITransactionBuilder _transactionBuilder = null!;
        private ITransactionPool _transactionPool = null!;
        private IStateManager _stateManager = null!;
        private IPrivateWallet _wallet = null!;
        private IConfigManager _configManager = null!;
        private INetworkManager _networkManager = null!;
        private ISnapshotIndexRepository _snapshotIndexer = null!;
        private INodeRetrieval _nodeRetrieval = null!;
        private IRocksDbContext _dbContext = null!;
        private IContainer? _container;

        [SetUp]
        public void Setup()
        {
            TestUtils.DeleteTestChainData();
            var containerBuilder = new SimpleInjectorContainerBuilder(new ConfigManager(
                Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.json"),
                new RunOptions()
            ));
            containerBuilder.RegisterModule<BlockchainModule>();
            containerBuilder.RegisterModule<ConfigModule>();
            containerBuilder.RegisterModule<StorageModule>();
            containerBuilder.RegisterModule<NetworkModule>();
            containerBuilder.RegisterModule<ConsensusModule>();
            _container = containerBuilder.Build();
            _blockManager = _container.Resolve<IBlockManager>();
            _transactionBuilder = _container.Resolve<ITransactionBuilder>();
            _stateManager = _container.Resolve<IStateManager>();
            _wallet = _container.Resolve<IPrivateWallet>();
            _transactionPool = _container.Resolve<ITransactionPool>();
            _configManager = _container.Resolve<IConfigManager>();
            _networkManager = _container.Resolve<INetworkManager>();
            _snapshotIndexer = _container.Resolve<ISnapshotIndexRepository>();
            _nodeRetrieval = _container.Resolve<INodeRetrieval>();
            _dbContext = _container.Resolve<IRocksDbContext>();
            // set chainId from config
            if (TransactionUtils.ChainId == 0)
            {
                var chainId = _configManager.GetConfig<NetworkConfig>("network")?.ChainId;
                TransactionUtils.SetChainId((int)chainId!);
            }
            _blockManager.TryBuildGenesisBlock();
        }

        [TearDown]
        public void Teardown()
        {
            _container?.Dispose();
            TestUtils.DeleteTestChainData();
        }

        [Test]
        [Repeat(2)]
        public void Test_BlockBatchRequestAndReply()
        {
            ulong totalBlocks = 10;
            GenerateBlocks(totalBlocks);
            var blocks = new List<ulong>();
            for (ulong i = 1 ; i <= totalBlocks; i++)
            {
                blocks.Add(i);
            }
            var message = _networkManager.MessageFactory.BlockBatchRequest(blocks);
            CheckBlockBatchRequest(message);
        }

        [Test]
        [Repeat(2)]
        public void Test_RootHashByTrieNameRequestAndReply()
        {
            ulong totalBlocks = 10;
            GenerateBlocks(totalBlocks);
            string[] snapshotNames = new string[] { "Balances", "Contracts", "Storage", "Transactions", "Blocks", "Events", "Validators" };
            for (ulong i = 1 ; i <= totalBlocks; i++)
            {
                foreach (var snapshotName in snapshotNames)
                {
                    var message = _networkManager.MessageFactory.RootHashByTrieNameRequest(i , snapshotName);
                    CheckRootHashByTrieNameRequest(message, i , snapshotName);
                }
            }
            
        }

        [Test]
        [Repeat(2)]
        public void Test_TrieNodeByHashRequestAndReply()
        {
            ulong totalBlocks = 10;
            GenerateBlocks(totalBlocks);
            
            for (ulong blockNo = 1; blockNo <= totalBlocks ; blockNo++)
            {
                var blockchainSnapshot = _snapshotIndexer.GetSnapshotForBlock(blockNo);
                var snapshots = blockchainSnapshot.GetAllSnapshot();
                foreach (var snapshot in snapshots)
                {
                    var nodeHashList = new List<UInt256>();
                    var state = snapshot.GetState();
                    foreach (var item in state)
                    {
                        var node = item.Value;
                        nodeHashList.Add(node.Hash.ToUInt256());
                    }

                    var message = _networkManager.MessageFactory.TrieNodeByHashRequest(nodeHashList);

                    CheckTrieNodeByHashRequest(message, snapshot);
                }
            }
        }

        public void CheckBlockBatchRequest(NetworkMessage message)
        {
            // most of it copy pasted from OnBlockBatchRequest from MessageHandler.cs 
            
            Logger.LogTrace("Start processing OnBlockBatchRequest");
            var request = message.BlockBatchRequest;
            try
            {
                var blockNumbers = request.BlockNumbers.ToList();
                List<Block> blockBatch = new List<Block>();
                foreach (var blockNumber in blockNumbers)
                {
                    var block = _blockManager.GetByHeight(blockNumber);
                    if (block != null) blockBatch.Add(block);
                }
                var reply = new BlockBatchReply
                {
                    BlockBatch = {blockBatch}
                };
                CheckBlockBatchReply(reply);
            }
            catch (Exception exception)
            {
                Logger.LogWarning($"Got exception trying to get blocks : {exception}");
                var reply = new BlockBatchReply
                {
                    BlockBatch = {new List<Block>()}
                };
            }
            
            Logger.LogTrace("Finished processing OnBlockBatchRequest");
        }
        public void CheckBlockBatchReply(BlockBatchReply reply)
        {
            var blocks = reply.BlockBatch.ToList();
            foreach (var block in  blocks)
            {
                var blockId = block.Header.Index;
                var sameBlock = _blockManager.GetByHeight(blockId);
                Assert.AreEqual(block, sameBlock, "block from reply and block from blockmanager did not match");
            }
        }

        public void CheckRootHashByTrieNameRequest(NetworkMessage message, ulong block, string snapshotName)
        {
            // most of it copy pasted from OnRootHashByTrieNameRequest from MessageHandler.cs 

            Logger.LogTrace("Start processing OnRootHashByTrieNameRequest");
            var request = message.RootHashByTrieNameRequest;
            try
            {
                var blockchainSnapshot = _snapshotIndexer.GetSnapshotForBlock(request.Block);
                var snapshot = blockchainSnapshot.GetSnapshot(request.TrieName);
                var reply = new RootHashByTrieNameReply
                {
                    RootHash = (snapshot is null) ? UInt256Utils.Zero : snapshot.Hash
                };
                CheckRootHashByTrieNameReply(reply, block, snapshotName);
            }
            catch (Exception exception)
            {
                Logger.LogWarning($"Got exception trying to get root hash for trie {request.TrieName}"
                    + $" for block {request.Block} : {exception}");
                var reply = new RootHashByTrieNameReply
                {
                    RootHash = UInt256Utils.Zero
                };
            }

            Logger.LogTrace("Finished processing OnRootHashByTrieNameRequest");
        }

        public void CheckRootHashByTrieNameReply(RootHashByTrieNameReply reply, ulong blockIndex, string snapshotName)
        {
            var snapshotRootHash = reply.RootHash;
            var blockchainSnapshot = _snapshotIndexer.GetSnapshotForBlock(blockIndex);
            var snapshot = blockchainSnapshot.GetSnapshot(snapshotName);
            Assert.AreEqual(snapshotRootHash , snapshot!.Hash, "snapshot root hash does not match");
        }

        public void CheckTrieNodeByHashRequest(NetworkMessage message, ISnapshot snapshot)
        {
            // most of it copy pasted from OnTrieNodeByHashRequest from MessageHandler.cs 

            Logger.LogTrace("Start processing OnTrieNodeByHashRequest");
            var request = message.TrieNodeByHashRequest;
            var nodeHashes = request.NodeHashes.ToList();

            var trieNodeInfoList = new List<TrieNodeInfo>();
            foreach (var nodeHash in nodeHashes)
            {
                IHashTrieNode? node = _nodeRetrieval.TryGetNode(nodeHash.ToBytes(), out var childrenHash);
                if (node is null) continue;
                var nodeInfo = new TrieNodeInfo();

                switch (node)
                {
                    case InternalNode internalNode:
                        var childrenHashes = new List<UInt256>();
                        foreach(var childHash in childrenHash) childrenHashes.Add(childHash.ToUInt256());
                        nodeInfo.NonLeaf = new InternalNodeInfo
                        {
                            NodeType = ByteString.CopyFrom((byte) internalNode.Type),
                            Hash = internalNode.Hash.ToUInt256(),
                            ChildrenMask = internalNode.ChildrenMask,
                            ChildrenHash = { childrenHashes }
                        };
                        break;

                    case LeafNode leafNode:
                        nodeInfo.Leaf = new LeafNodeInfo
                        {
                            NodeType = ByteString.CopyFrom((byte) leafNode.Type),
                            Hash = leafNode.Hash.ToUInt256(),
                            KeyHash = leafNode.KeyHash.ToUInt256(),
                            Value = ByteString.CopyFrom(leafNode.Value)
                        };
                        break;
                }
                trieNodeInfoList.Add(nodeInfo);
            }

            var reply = new TrieNodeByHashReply
            {
                TrieNodes = {trieNodeInfoList}
            };
            CheckTrieNodeByHashReply(reply, snapshot);
            
            Logger.LogTrace("Finished processing OnTrieNodeByHashRequest");
        }

        public void CheckTrieNodeByHashReply(TrieNodeByHashReply reply , ISnapshot snapshot)
        {
            var state = snapshot.GetState();
            var nodeInfoList = reply.TrieNodes.ToList();
            var nodeList = new List < IHashTrieNode > ();
            var nodeListFromState = new List < IHashTrieNode > ();
            foreach (var nodeInfo in nodeInfoList)
            {
                switch (nodeInfo.MessageCase)
                {
                    case TrieNodeInfo.MessageOneofCase.Leaf:
                        var leafNode = GetLeafNodeFromInfo(nodeInfo.Leaf);
                        nodeList.Add(leafNode);
                        break;
                    
                    case TrieNodeInfo.MessageOneofCase.NonLeaf:
                        var internalNode = GetInternalNodeFromInfo(nodeInfo.NonLeaf);
                        nodeList.Add(internalNode);
                        break;

                    default:
                        Assert.That(false, "Invalid trie node in nodeInfo");
                        break;
                }
            }
            foreach (var item in state)
            {
                nodeListFromState.Add(item.Value);
                Assert.That(!nodeList.Any(node => node == item.Value) , "node from state not found in reply");
            }
            foreach (var node in nodeList)
            {
                Assert.That(!nodeListFromState.Any(stateNode => stateNode == node) , "node from reply not found in state");
            }
        }

        public LeafNode GetLeafNodeFromInfo(LeafNodeInfo info)
        {
            var keyHash = info.KeyHash.ToBytes();
            var leafNode = new LeafNode(keyHash , info.Value);
            Assert.AreEqual(leafNode.Hash , info.Hash.ToBytes(), "node hash mismatch");
            return leafNode;
        }

        public InternalNode GetInternalNodeFromInfo(InternalNodeInfo info)
        {
            var childrenHash = info.ChildrenHash.ToList();
            var childrenId = new List<ulong>();
            var childrenHashBytes = new List<byte[]>();
            foreach (var childHash in childrenHash)
            {
                var nodeIdBytes = _dbContext.Get(EntryPrefix.VersionByHash.BuildPrefix(childHash.ToBytes()));
                childrenId.Add(UInt64Utils.FromBytes(nodeIdBytes));
                childrenHashBytes.Add(childHash.ToBytes());
            }
            var internalNode = new InternalNode(info.ChildrenMask, childrenId, childrenHashBytes);
            Assert.AreEqual(internalNode.Hash , info.Hash.ToBytes(), "node hash mismatch");
            return internalNode;
        }
        
        private void GenerateBlocks(ulong blockNum)
        {
            for (ulong i = 0; i < blockNum; i++)
            {
                var txs = GetCurrentPoolTxs();
                var block = BuildNextBlock(txs);
                var result = ExecuteBlock(block, txs);
                Assert.AreEqual(OperatingError.Ok, result);
            }
        }

        private TransactionReceipt[] GetCurrentPoolTxs()
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

            var height = _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight();
            var predecessor =
                _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(height);

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
                keyPair.PrivateKey.Encode()
            ).ToSignature();

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

            var height = _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight();
            var predecessor =
                _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(height);
            var (header, multisig) = BuildHeaderAndMultisig(block.Header.MerkleRoot, predecessor, stateHash);

            block.Header = header;
            block.Multisig = multisig;
            block.Hash = header.Keccak();

            var status = _blockManager.Execute(block, receipts, true, true);
            return status;
        }
    }
}