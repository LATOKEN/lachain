using System;
using System.Linq;
using AustinHarris.JsonRpc;
using Lachain.Core.Blockchain.Hardfork;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Blockchain.SystemContracts;
using Lachain.Core.BlockchainFilter;
using Lachain.Core.Consensus;
using Lachain.Crypto;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Networking;
using Lachain.Storage.Repositories;
using Lachain.Storage.State;
using Lachain.Storage.Trie;
using Lachain.Utility;
using Lachain.Utility.JSON;
using Lachain.Utility.Utils;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;


namespace Lachain.Core.RPC.HTTP.Web3
{
    public class BlockchainServiceWeb3 : JsonRpcService
    {
        private static readonly ILogger<BlockchainServiceWeb3> Logger =
            LoggerFactory.GetLoggerForClass<BlockchainServiceWeb3>();

        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();

        private readonly ITransactionManager _transactionManager;
        private readonly IBlockManager _blockManager;
        private readonly IStateManager _stateManager;
        private readonly ITransactionPool _transactionPool;
        private readonly ISnapshotIndexRepository _snapshotIndexer;
        private readonly INetworkManager _networkManager;
        private readonly INodeRetrieval _nodeRetrieval;
        private readonly ISystemContractReader _systemContractReader;
        private readonly IConsensusManager _consensusManager;

        public BlockchainServiceWeb3(
            ITransactionManager transactionManager,
            IBlockManager blockManager,
            ITransactionPool transactionPool,
            IStateManager stateManager,
            ISnapshotIndexRepository snapshotIndexer,
            INetworkManager networkManager,
            INodeRetrieval nodeRetrieval,
            ISystemContractReader systemContractReader,
            IConsensusManager consensusManager)
        {
            _transactionPool = transactionPool;
            _transactionManager = transactionManager;
            _blockManager = blockManager;
            _stateManager = stateManager;
            _snapshotIndexer = snapshotIndexer;
            _networkManager = networkManager;
            _nodeRetrieval = nodeRetrieval;
            _systemContractReader = systemContractReader;
            _consensusManager = consensusManager;
        }

        [JsonRpcMethod("eth_getBlockByNumber")]
        public JObject? GetBlockByNumber(string blockTag, bool fullTx)
        {
            var blockNumber = GetBlockNumberByTag(blockTag);
            if (blockNumber == null)
                return null;
            var block = _blockManager.GetByHeight((ulong)blockNumber);
            if (block == null)
                return null;

            ulong gasUsed = 0;
            var txArray = new JArray();
            if (block.TransactionHashes.Count <= 0) 
                return Web3DataFormatUtils.Web3Block(block!, gasUsed, txArray);
            List<TransactionReceipt> txs = new List<TransactionReceipt>();
            try
            {
                foreach(var txHash in block.TransactionHashes)
                {
                    Logger.LogInformation($"Transaction hash {txHash.ToHex()} in block {blockNumber}");
                    TransactionReceipt? tx = _transactionManager.GetByHash(txHash);
                    if(tx is null)
                    {
                        Logger.LogWarning($"Transaction not found in DB {txHash.ToHex()}");
                    }
                    else txs.Add(tx);
                }
            }
            catch (Exception e)
            {
                Logger.LogWarning($"Exception {e}");
                Logger.LogWarning($"block {block!.Hash},  {block.Header.Index}, {block.TransactionHashes.Count}");
                foreach (var txhash in block.TransactionHashes)
                    Logger.LogWarning($"txhash {txhash.ToHex()}");
            }

            try
            {
                gasUsed = txs.Aggregate(gasUsed, (current, tx) => current + tx.GasUsed);
            }
            catch (Exception e)
            {
                Logger.LogWarning($"Exception {e}");
                Logger.LogWarning($"txs {txs}");
                foreach (var tx in txs)
                {
                    if (tx is null) 
                         continue;
                    Logger.LogWarning($"tx {tx.Hash.ToHex()} {tx.GasUsed} {tx.Status} {tx.IndexInBlock}");
                }
            }

            if(fullTx) 
                txArray = Web3DataFormatUtils.Web3BlockTransactionArray(txs, block!.Hash, block!.Header.Index);
            else
            {
                foreach (var tx in txs)
                {
                    txArray.Add(Web3DataFormatUtils.Web3Data(tx.Hash));
                }
            }
            return Web3DataFormatUtils.Web3Block(block!, gasUsed, txArray);
        }

        [JsonRpcMethod("la_getBlockRawByNumber")]
        public string? GetBlockRawByNumber(string blockTag) 
        {
            var blockNumber = GetBlockNumberByTag(blockTag);
            if (blockNumber == null)
                return null;
            Block? block = _blockManager.GetByHeight((ulong)blockNumber);
            if (block == null)
                return null;
            return Web3DataFormatUtils.Web3BlockRaw(block);
        }

        [JsonRpcMethod("la_getBlockRawByNumberBatch")]
        public JArray GetBlockRawByNumberBatch(List<string> blockTagList) 
        {
            JArray blockRawList = new JArray{};
            foreach(var blockTag in blockTagList)
            {
                var blockNumber = GetBlockNumberByTag(blockTag);
                if (blockNumber == null)
                    return null;
                Block? block = _blockManager.GetByHeight((ulong)blockNumber);
                if(block!=null)
                    blockRawList.Add(Web3DataFormatUtils.Web3BlockRaw(block));
            }
            return blockRawList;
        }



        [JsonRpcMethod("la_getStateByNumber")]
        public JObject? GetStateByNumber(string blockTag) 
        {
            var blockNumber = GetBlockNumberByTag(blockTag);
            if (blockNumber == null) return null;
            IBlockchainSnapshot blockchainSnapshot = _snapshotIndexer.GetSnapshotForBlock((ulong)blockNumber);
            var state = new JObject{};

            string[] trieNames = new string[] { "Balances", "Contracts", "Storage", "Transactions", "Blocks", "Events", "Validators" };
            ISnapshot[] snapshots = blockchainSnapshot.GetAllSnapshot();
            for(var i = 0; i < trieNames.Length; i++)
            {
                state[trieNames[i]] = Web3DataFormatUtils.Web3Trie(snapshots[i].GetState());
                state[trieNames[i] + "Root"] = Web3DataFormatUtils.Web3Number(snapshots[i].Version);
            }

            return state;
        }

        [JsonRpcMethod("la_checkNodeHashes")]
        public string CheckNodeHashes(string blockTag) 
        {
            var blockNumber = GetBlockNumberByTag(blockTag);
            IBlockchainSnapshot blockchainSnapshot = _snapshotIndexer.GetSnapshotForBlock((ulong)blockNumber);
            bool res = true;
            ISnapshot[] snapshots = blockchainSnapshot.GetAllSnapshot();
            foreach(var snapshot in snapshots) res &= snapshot.IsTrieNodeHashesOk();
            return Web3DataFormatUtils.Web3Number(Convert.ToUInt64(res));
        }

        [JsonRpcMethod("la_getStateHashFromTrieRootsRange")]
        public JObject? GetStateHashFromTrieRootsRange(string startBlockTag, string endBlockTag) 
        {
            ulong startBlock = startBlockTag.HexToUlong(), endBlock = endBlockTag.HexToUlong();
            var stateHash = new JObject { };
            for (ulong curBlock = startBlock; curBlock <= endBlock; curBlock++)
            {
                stateHash[curBlock.ToHex(false)] =
                    Web3DataFormatUtils.Web3Data(SingleNodeHashFromRoot(curBlock.ToHex(false)));
            }

            return stateHash;
        }

        [JsonRpcMethod("la_getStateHashFromTrieRoots")]
        public string GetStateHashFromTrieRoots(string blockTag) 
        {
            return Web3DataFormatUtils.Web3Data(SingleNodeHashFromRoot(blockTag));
        }

        [JsonRpcMethod("la_getAllTriesHash")]
        public JObject? GetAllTrieRootsHash(string blockTag) 
        {
            var blockNumber = GetBlockNumberByTag(blockTag);
            IBlockchainSnapshot blockchainSnapshot = _snapshotIndexer.GetSnapshotForBlock((ulong)blockNumber);
            var trieRootsHash = new JObject{};
            ISnapshot[] snapshots = blockchainSnapshot.GetAllSnapshot();
            string[] snapshotNames = new string[] { "Balances", "Contracts", "Storage", "Transactions", "Blocks", "Events", "Validators" };
            for(int i = 0; i<snapshots.Length; i++)
            {
                trieRootsHash[snapshotNames[i]+"Hash"] = Web3DataFormatUtils.Web3Data(snapshots[i].Hash);
            }
            return trieRootsHash;
        }

        [JsonRpcMethod("la_getNodeByHash")]
        public JObject GetNodeByHash(string nodeHash) 
        {
            IHashTrieNode? node = _nodeRetrieval.TryGetNode(HexUtils.HexToBytes(nodeHash), out var childrenHash);
            if (node == null) return new JObject { };
            return Web3DataFormatUtils.Web3NodeWithChildrenHash(node, childrenHash);
        }

        [JsonRpcMethod("la_getNodeByHashBatch")]
        public JArray GetNodeByHashBatch(List<string> nodeHashList)
        {
            JArray nodeList = new JArray{};
            foreach(var nodeHash in nodeHashList)
            {
                JObject node = GetNodeByHash(nodeHash);
                nodeList.Add(node);
            }
            return nodeList;
        }

        [JsonRpcMethod("la_getChildrenByHash")]
        public JObject GetChildrenByHash(string nodeHash) 
        {
            IHashTrieNode? node = _nodeRetrieval.TryGetNode(HexUtils.HexToBytes(nodeHash), out var childrenHash);
            if (node == null) return new JObject { };

            JArray children = new JArray { };
            foreach(var childHash in childrenHash)
            {
                JObject child = GetNodeByHash(Web3DataFormatUtils.Web3Data(childHash));
                if (child.Count == 0) return new JObject { };
                children.Add(child);
            }

            JObject nodeHashWithChildren = new JObject { };
            nodeHashWithChildren[Web3DataFormatUtils.Web3Data(node.Hash)] = children;
            return nodeHashWithChildren;
        }

        [JsonRpcMethod("la_getChildrenByHashBatch")]
        public JArray GetChildrenByHashBatch(List<string> nodeHashList)
        {
            JArray childrenList = new JArray{};
            foreach(var nodeHash in nodeHashList)
            {
                JObject nodeHashWithChildren = GetChildrenByHash(nodeHash);
                childrenList.Add(nodeHashWithChildren);
            }
            return childrenList;
        }

        [JsonRpcMethod("la_getRootHashByTrieName")]
        public string GetRootHashByTrieName(string trieName, string blockTag) 
        {
            var blockNumber = GetBlockNumberByTag(blockTag);
            if (blockNumber == null) return "0x";
            IBlockchainSnapshot blockchainSnapshot = _snapshotIndexer.GetSnapshotForBlock((ulong)blockNumber);
            var snapshot = blockchainSnapshot.GetSnapshot(trieName);
            if (snapshot == null) return "0x";
            return Web3DataFormatUtils.Web3Data(snapshot.Hash);
        }

        [JsonRpcMethod("la_getNodeByVersion")]
        public JObject GetNodeByVersion(string versionTag) 
        {
            var version = GetVersionNumberByTag(versionTag);
            if (version == null) return new JObject { };
            var node = _nodeRetrieval.TryGetNode((ulong)version);
            if (node == null) return new JObject { };
            return Web3DataFormatUtils.Web3Node(node);
        }
        [JsonRpcMethod("la_getChildrenByVersion")]
        public JObject GetChildrenByVersion(string versionTag) 
        {
            var version = GetVersionNumberByTag(versionTag);
            if (version == null) return new JObject { };
            var node = _nodeRetrieval.TryGetNode((ulong)version);
            if (node == null) return new JObject { };

            JArray children = new JArray { };

            if (node.Type == NodeType.Internal)
            {
                foreach (var childId in node.Children)
                {
                    var child = _nodeRetrieval.TryGetNode(childId);
                    if (child == null) return new JObject { };
                    children.Add(Web3DataFormatUtils.Web3Node(child));
                }
            }
            JObject nodeWithChildren = new JObject { };
            nodeWithChildren[Web3DataFormatUtils.Web3Number((ulong)version)] = children;
            return nodeWithChildren;
        }

        [JsonRpcMethod("la_getChildrenByVersionBatch")]
        public JArray GetChildrenByVersionBatch(List<string> versionTags)
        {
            JArray childrenBatch = new JArray {};
            foreach (var versionTag in versionTags)
            {
                var children = GetChildrenByVersion(versionTag);
                childrenBatch.Add(children);
            }
            return childrenBatch;
        }

        [JsonRpcMethod("la_getRootVersionByTrieName")]
        public string GetRootVersionByTrieName(string trieName, string blockTag)
        {
            var blockNumber = GetBlockNumberByTag(blockTag);
            if (blockNumber == null) return "0x";
            IBlockchainSnapshot blockchainSnapshot = _snapshotIndexer.GetSnapshotForBlock((ulong)blockNumber);
            ISnapshot? snapshot =  blockchainSnapshot.GetSnapshot(trieName);
            if (snapshot is null) return "0x";
            else return Web3DataFormatUtils.Web3Number(snapshot.Version);
        }

        [JsonRpcMethod("eth_getBlockByHash")]
        public JObject? GetBlockByHash(string blockHash , bool fullTx = true) 
        {
            var block = _blockManager.GetByHash(blockHash.HexToBytes().ToUInt256());
            if (block == null)
                return null;
            
            List<TransactionReceipt> txs = new List<TransactionReceipt>();
            try
            {
                foreach(var txHash in block.TransactionHashes)
                {
                    Logger.LogInformation($"Transaction hash {txHash.ToHex()} in block {block.Header.Index}");
                    TransactionReceipt? tx = _transactionManager.GetByHash(txHash);
                    if(tx is null)
                    {
                        Logger.LogWarning($"Transaction not found in DB {txHash.ToHex()}");
                    }
                    else txs.Add(tx);
                }
            }
            catch (Exception e)
            {
                Logger.LogWarning($"Exception {e}");
                Logger.LogWarning($"block {block!.Hash},  {block.Header.Index}, {block.TransactionHashes.Count}");
                foreach (var txhash in block.TransactionHashes)
                    Logger.LogWarning($"txhash {txhash.ToHex()}");
            }

            ulong gasUsed = 0;
            try
            {
                gasUsed = txs.Aggregate(gasUsed, (current, tx) => current + tx.GasUsed);
            }
            catch (Exception e)
            {
                Logger.LogWarning($"Exception {e}");
                Logger.LogWarning($"txs {txs}");
                foreach (var tx in txs)
                {
                    if (tx is null) 
                         continue;
                    Logger.LogWarning($"tx {tx.Hash.ToHex()} {tx.GasUsed} {tx.Status} {tx.IndexInBlock}");
                }
            }
            
            var txArray = fullTx
                ? Web3DataFormatUtils.Web3BlockTransactionArray(txs, block!.Hash, block!.Header.Index)
                : new JArray();
            if (!fullTx)
            {
                foreach (var tx in txs)
                {
                    txArray.Add(Web3DataFormatUtils.Web3Data(tx.Hash));
                }
            }
            return Web3DataFormatUtils.Web3Block(block!, gasUsed, txArray);
        }

        [JsonRpcMethod("eth_getTransactionsByBlockHash")]
        public JObject? GetTransactionsByBlockHash(string blockHash)
        {
            var block = _blockManager.GetByHash(blockHash.HexToBytes().ToUInt256());
            if (block is null)
                return new JObject { ["transactions"] = new JArray() };
            var txs = block.TransactionHashes
                .Select(hash => _transactionManager.GetByHash(hash)?.ToJson())
                .ToList();
            return new JObject
            {
                ["transactions"] = new JArray(txs),
            };
        }

        [JsonRpcMethod("eth_getBlockTransactionCountByNumber")]
        public string? GetBlockTransactionsCountByNumber(string blockTag) 
        {
            var blockNumber = GetBlockNumberByTag(blockTag);
            if (blockNumber == null) 
                return Web3DataFormatUtils.Web3Number(_transactionPool.Size());
            var block = _blockManager.GetByHeight((ulong)blockNumber);
            return block == null ? null : Web3DataFormatUtils.Web3Number((ulong)block!.TransactionHashes.Count);
        }

        [JsonRpcMethod("eth_getBlockTransactionCountByHash")]
        public string? GetBlockTransactionsCountByHash(string blockHash) 
        {
            var block = _blockManager.GetByHash(blockHash.HexToUInt256());
            return block == null ? null : Web3DataFormatUtils.Web3Number((ulong)block!.TransactionHashes.Count);
        }

        [JsonRpcMethod("eth_blockNumber")]
        public string GetBlockNumber()
        {
            return Web3DataFormatUtils.Web3Number(_blockManager.GetHeight());
        }

        [JsonRpcMethod("la_getDownloadedNodesTillNow")]
        public string GetDownloadedNodesTillNow() 
        {
            return Web3DataFormatUtils.Web3Number(_nodeRetrieval.GetDownloadedNodeCount());
        }

        [JsonRpcMethod("la_validator_info")]
        public JObject GetValidatorInfo(string publicKeyStr) 
        {
            var publicKey = publicKeyStr.HexToBytes();
            var addressUint160 = Crypto.ComputeAddress(publicKey).ToUInt160();

            var balance = _stateManager.CurrentSnapshot.Balances.GetBalance(addressUint160);

            var stake = _systemContractReader.GetStake(addressUint160).ToMoney();
            var penalty = _systemContractReader.GetPenalty(addressUint160).ToMoney();

            var isNextValidator = _systemContractReader.IsNextValidator(publicKey);
            var isAbleToBeValidator = _systemContractReader.IsAbleToBeValidator(addressUint160);
            var isPreviousValidator = _systemContractReader.IsPreviousValidator(publicKey);
            var isCurrentValidator = _stateManager.CurrentSnapshot.Validators
                .GetValidatorsPublicKeys().Any(pk =>
                    pk.Buffer.ToByteArray().SequenceEqual(publicKey));
            
            var isAbleToBeStaker = balance.ToWei() > StakingContract.TokenUnitsInRoll;
            var isStaker = !_systemContractReader.GetStake(addressUint160).IsZero();

            bool stakeDelegated = !isStaker && isCurrentValidator;

            string state;
            if (isCurrentValidator)
                state = "Validator";
            else if (isNextValidator)
                state = "NextValidator";
            else if (isAbleToBeValidator)
                state = "AbleToBeValidator";
            else if (isPreviousValidator)
                state = "PreviousValidator";
            else if (isAbleToBeStaker)
                state = "AbleToBeStaker";
            else state = "Newbie";

            return new JObject
            {
                ["address"] = addressUint160.ToHex(),
                ["publicKey"] = publicKey.ToHex(),
                ["balance"] = balance.ToString(),
                ["stake"] = stake.ToString(),
                ["penalty"] = penalty.ToString(),
                ["state"] = state,
                ["stakeDelegated"] = stakeDelegated.ToString(),
                ["staker"] = isStaker
            };
        }

        [JsonRpcMethod("eth_getUncleCountByBlockHash")]
        private ulong GetUncleCountByBlockHash(string blockHash)
        {
            return 0;
        }

        [JsonRpcMethod("eth_getUncleCountByBlockNumber")]
        private ulong GetUncleCountByBlockNumber(string blockTag)
        {
            return 0;
        }

        [JsonRpcMethod("eth_getUncleByBlockHashAndIndex")]
        private JObject? GetUncleByBlockHashAndIndex(string blockHash, ulong index)
        {
            return null;
        }

        [JsonRpcMethod("eth_getUncleByBlockNumberAndIndex")]
        private JObject? GetUncleByBlockNumberAndIndex(string blockTag, ulong index)
        {
            return null;
        }

        [JsonRpcMethod("eth_getEventsByTransactionHash")]
        public JArray GetEventsByTransactionHash(string txHash) 
        {
            var transactionHash = txHash.HexToUInt256();
            var receipt = _stateManager.LastApprovedSnapshot.Transactions.GetTransactionByHash(transactionHash);
            if (receipt is null) return new JArray();
            var block = _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(receipt!.Block);
            if (block is null) return new JArray(); // ???
            var txEvents = _stateManager.LastApprovedSnapshot.Events.GetTotalTransactionEvents(transactionHash);
            var jArray = new JArray();
            for (var i = 0; i < txEvents; i++)
            {
                var evObj = _stateManager.LastApprovedSnapshot.Events.GetEventByTransactionHashAndIndex(transactionHash,
                    (uint)i);
                var ev = evObj.Event;
                if (ev is null)
                    continue;
                jArray.Add(Web3DataFormatUtils.Web3Event(evObj, receipt!.Block, block!.Hash));
            }

            return jArray;
        }

        [JsonRpcMethod("eth_getLogs")]
        public JArray GetLogs(JObject opts)
        {
            var fromBlock = opts["fromBlock"];
            var toBlock = opts["toBlock"];
            var address = opts["address"];
            var topicsJson = opts["topics"];
            var blockhash = opts["blockHash"];


            if ((!(fromBlock is null) || !(toBlock is null)) && !(blockhash is null))
                throw new RpcException(
                    RpcErrorCode.Error,
                    "If blockHash is present in in the filter criteria, then neither fromBlock nor toBlock are allowed."
                );

            ulong? start = _blockManager.GetHeight();
            if (!(fromBlock is null))
            {
                start = GetBlockNumberByTag((string)fromBlock!);
            }

            ulong? finish = _blockManager.GetHeight();
            if (!(toBlock is null))
            {
                finish = GetBlockNumberByTag((string)toBlock!);
            }

            if (!(blockhash is null))
            {
                var hash = ((string)blockhash!).HexToBytes().ToUInt256();
                var block = _blockManager.GetByHash(hash);
                if (block is null)
                    return new JArray();
                start = block!.Header.Index;
                finish = block!.Header.Index;
            }

            Logger.LogInformation($"Check blocks from {start} to {finish}");
            
            var allTopics = new List<List<UInt256>>();
            if (!(topicsJson is null))
            {
                allTopics = BlockchainFilterUtils.GetTopics(topicsJson);
            }
            while(allTopics.Count < 4) allTopics.Add(new List<UInt256>());

            var addresses = new List<UInt160>();
            if (!(address is null))
            {
                switch (address)
                {
                    case JArray arrayAddr:
                        addresses = BlockchainFilterUtils.GetAddresses(arrayAddr);
                        break;
                    case JValue valueAddr:
                        addresses.Add(((string?)valueAddr ?? 
                            throw new RpcException(RpcErrorCode.Error, "Invalid address value")).HexToUInt160());
                        break;
                    default:
                        throw new RpcException(RpcErrorCode.Error, $"Invalid address value: {address}");
                }
            }

            if((start is null) || (finish is null)) return new JArray();

            return GetLogs((ulong)start , (ulong)finish , addresses , allTopics);
        }



        [JsonRpcMethod("eth_getTransactionPool")]
        public JArray GetTransactionPool() 
        {
            var txHashes = _transactionPool.Transactions.Keys;
            var jArray = new JArray();
            foreach (var txHash in txHashes)
            {
                jArray.Add(txHash.ToHex());
            }

            return jArray;
        }

        [JsonRpcMethod("eth_chainId")]
        public string ChainId() 
        {
            return TransactionUtils.ChainId(HardforkHeights.IsHardfork_9Active(_blockManager.GetHeight() + 1)).ToHex();
        }

        [JsonRpcMethod("net_version")]
        public string NetVersion() 
        {
            return TransactionUtils.ChainId(HardforkHeights.IsHardfork_9Active(_blockManager.GetHeight() + 1)).ToHex();
        }


        [JsonRpcMethod("eth_getTransactionPoolByHash")]
        public JObject? GetTransactionPoolByHash(string txHash) 
        {
            var transaction = _transactionPool.GetByHash(txHash.HexToUInt256());
            return transaction?.ToJson();
        }

        [JsonRpcMethod("eth_getStorageAt")]
        public string GetStorageAt(string address, string position, string blockTag)
        {

            var blockNumber = GetBlockNumberByTag(blockTag);
            var blockchainSnapshot = _snapshotIndexer.GetSnapshotForBlock((ulong)blockNumber!);
            var value = blockchainSnapshot.Storage.GetValue(address.HexToUInt160(), position.HexToUInt256());
            return Web3DataFormatUtils.Web3Data(value.ToHex().HexToBytes());

        }

        [JsonRpcMethod("eth_getWork")]
        private JArray GetWork()
        {
            return new JArray(
                Web3DataFormatUtils.Web3Data(new UInt256()),
                Web3DataFormatUtils.Web3Data(new UInt256()),
                Web3DataFormatUtils.Web3Data(new UInt256())
            );
        }

        [JsonRpcMethod("eth_submitWork")]
        private bool SubmitWork(string p1, string p2, string p3)
        {
            return false;
        }

        [JsonRpcMethod("eth_submitHashrate")]
        private bool SubmitHashrate(string p1, string p2)
        {
            return false;
        }
        
        [JsonRpcMethod("la_getLatestValidators")]
        private JArray GetCurrentValidators()
        {
            var allValidators = _stateManager.CurrentSnapshot.Validators.GetValidatorsPublicKeys().ToArray();
            JArray validators = new JArray { };
            foreach (var validator in allValidators)
            {
                validators.Add(Web3DataFormatUtils.Web3Data(validator.Buffer.ToByteArray()));
            }
            return validators;
        }
        
        [JsonRpcMethod("la_consensusState")]
        private JArray GetConsensusState()
        {
            var broadcaster = _consensusManager.GetEraBroadcaster();
            JArray protocols = new JArray();
            
            if (broadcaster != null)
            {
                var registry = broadcaster.GetRegistry();
            
                foreach (var id in registry.Keys)
                {
                    if(!registry[id].Terminated)
                    {
                        var temp = new JObject
                        {
                            ["protocol"] = id.ToString()
                        };
                    
                        protocols.Add(temp);
                    }
                }
            }
            return protocols;
        }

        [JsonRpcMethod("la_getValidatorsAfterBlock")]
        public JArray GetValidatorsAfterBlock(string blockTag)
        {
            if (blockTag == "pending")
                blockTag = "latest"; // current validators are the validators after latest block
            var blockNum = GetBlockNumberByTag(blockTag);
            try
            {
                var validators = _snapshotIndexer.GetSnapshotForBlock(blockNum!.Value).Validators.GetValidatorsPublicKeys().ToArray();
                var result = new JArray();
                foreach (var publicKey in validators)
                {
                    result.Add(Web3DataFormatUtils.Web3Data(publicKey.EncodeCompressed()));
                }
                return result;
            }
            catch (Exception exception)
            {
                Logger.LogWarning($"Exception occured trying to get validators after block {blockNum!.Value}: {exception}");
                return new JArray();
            }
            
        }

        private ulong? GetBlockNumberByTag(string blockTag)
        {
            return blockTag switch
            {
                "latest" => _blockManager.GetHeight(),
                "earliest" => 0,
                "pending" => null,
                _ => blockTag.HexToUlong()
            };
        }

        private ulong? GetVersionNumberByTag(string versionTag)
        {
            return versionTag.HexToUlong();
        }

        private UInt256 SingleNodeHashFromRoot(string blockTag)
        {
            var blockNumber = GetBlockNumberByTag(blockTag);
            IBlockchainSnapshot blockchainSnapshot = _snapshotIndexer.GetSnapshotForBlock((ulong)blockNumber);
            return blockchainSnapshot.StateHash;
        }

        public JArray GetLogs(ulong start, ulong finish, List<UInt160> addresses, List<List<UInt256>> allTopics)
        {
            var jArray = new JArray();
            for (var blockNumber = start; blockNumber <= finish; blockNumber++)
            {
                var block = _blockManager.GetByHeight(blockNumber);
                if (block == null)
                    continue;
                var txs = block!.TransactionHashes;
                foreach (var tx in txs)
                {
                    

                    var txEvents = _stateManager.LastApprovedSnapshot.Events.GetTotalTransactionEvents(tx);
                    for (var i = 0; i < txEvents; i++)
                    {
                        var txEventObj = _stateManager.LastApprovedSnapshot.Events.GetEventByTransactionHashAndIndex(tx,
                            (uint)i);
                        var txEvent = txEventObj.Event;
                        if (txEvent is null)
                            continue;
                        
                        if(!addresses.Any(a => txEvent.Contract.Equals(a))) continue;

                        var txTopics = new List<UInt256>();
                        txTopics.Add(txEvent.SignatureHash);
                        if(txEventObj.Topics != null){
                            foreach(var topic in txEventObj.Topics) 
                            {
                                txTopics.Add(topic);
                            }
                        }

                        if (!MatchTopics(allTopics, txTopics))
                        {
                            Logger.LogInformation($"Skip event with signature [{txEvent.SignatureHash.ToHex()}]");
                            continue;
                        }

                        if (txEvent.BlockHash is null || txEvent.BlockHash.IsZero())
                        {
                            txEvent.BlockHash = block.Hash;
                        }
           
                        jArray.Add(Web3DataFormatUtils.Web3Event(txEventObj, blockNumber,block.Hash));
                    }
                }
            }

            return jArray;
        }

        public bool MatchTopics(List<List<UInt256>> allTopics, List<UInt256> txTopics)
        {
            for(int i = 0 ; i < 4 ; i++){
                if(allTopics[i].Count > 0){
                    if(txTopics.Count <= i) return false;
                    if(!allTopics[i].Any(t => txTopics[i].Equals(t))) return false;
                }
            }
            return true;
        }
    }
}