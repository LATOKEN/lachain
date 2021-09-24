using System;
using System.Linq;
using AustinHarris.JsonRpc;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Pool;
using Lachain.Crypto;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Networking;
using Lachain.Storage.Repositories;
using Lachain.Storage.State;
using Lachain.Utility.JSON;
using Lachain.Utility.Utils;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Globalization;
using Lachain.Core.Blockchain.SystemContracts;
using Lachain.Storage.Trie;
using Lachain.Utility;


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

        public BlockchainServiceWeb3(
            ITransactionManager transactionManager,
            IBlockManager blockManager,
            ITransactionPool transactionPool,
            IStateManager stateManager,
            ISnapshotIndexRepository snapshotIndexer,
            INetworkManager networkManager,
            INodeRetrieval nodeRetrieval,
            ISystemContractReader systemContractReader)
        {
            _transactionPool = transactionPool;
            _transactionManager = transactionManager;
            _blockManager = blockManager;
            _stateManager = stateManager;
            _snapshotIndexer = snapshotIndexer;
            _networkManager = networkManager;
            _nodeRetrieval = nodeRetrieval;
            _systemContractReader = systemContractReader;
        }

        [JsonRpcMethod("eth_getBlockByNumber")]
        private JObject? GetBlockByNumber(string blockTag, bool fullTx)
        {
            var blockNumber = GetBlockNumberByTag(blockTag);
            if (blockNumber == null)
                return null;
            var block = _blockManager.GetByHeight((ulong)blockNumber);
            if (block == null)
                return null;
            var txs = block!.TransactionHashes
                .Select(hash => _transactionManager.GetByHash(hash)!)
                .ToList();
            var gasUsed = txs.Aggregate<TransactionReceipt, ulong>(0, (current, tx) => current + tx.GasUsed);
            var txArray = fullTx
                ? Web3DataFormatUtils.Web3BlockTransactionArray(txs, block!.Hash, block!.Header.Index)
                : new JArray();
            return Web3DataFormatUtils.Web3Block(block!, gasUsed, txArray);
        }

        [JsonRpcMethod("la_getBlockRawByNumber")]
        private string? GetBlockRawByNumber(string blockTag)
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
        private JArray GetBlockRawByNumberBatch(List<string> blockTagList)
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
        private JObject? GetStateByNumber(string blockTag)
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
        private string CheckNodeHashes(string blockTag)
        {
            var blockNumber = GetBlockNumberByTag(blockTag);
            IBlockchainSnapshot blockchainSnapshot = _snapshotIndexer.GetSnapshotForBlock((ulong)blockNumber);
            bool res = true;
            ISnapshot[] snapshots = blockchainSnapshot.GetAllSnapshot();
            foreach(var snapshot in snapshots) res &= snapshot.IsTrieNodeHashesOk();
            return Web3DataFormatUtils.Web3Number(Convert.ToUInt64(res));
        }

        [JsonRpcMethod("la_getStateHashFromTrieRootsRange")]
        private JObject? GetStateHashFromTrieRootsRange(string startBlockTag, string endBlockTag)
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
        private string GetStateHashFromTrieRoots(string blockTag)
        {
            return Web3DataFormatUtils.Web3Data(SingleNodeHashFromRoot(blockTag));
        }

        [JsonRpcMethod("la_getAllTriesHash")]
        private JObject? GetAllTrieRootsHash(string blockTag)
        {
            var blockNumber = GetBlockNumberByTag(blockTag);
            IBlockchainSnapshot blockchainSnapshot = _snapshotIndexer.GetSnapshotForBlock((ulong)blockNumber);
            var trieRootsHash = new JObject{};
            ISnapshot[] snapshots = blockchainSnapshot.GetAllSnapshot();
            string[] snapshotNames = new string[] { "Balances", "Contracts", "Storage", "Transactions", "Blocks", "Events", "Validators" };
            for(int i = 0; i<snapshots.Length; i++)
            {
                trieRootsHash[snapshots[i]+"Hash"] = Web3DataFormatUtils.Web3Data(snapshots[i].Hash);
            }
            return trieRootsHash;
        }

        [JsonRpcMethod("la_getNodeByHash")]
        private JObject GetNodeByHash(string nodeHash)
        {
            IHashTrieNode? node = _nodeRetrieval.TryGetNode(HexUtils.HexToBytes(nodeHash), out var childrenHash);
            if (node == null) return new JObject { };
            return Web3DataFormatUtils.Web3NodeWithChildrenHash(node, childrenHash);
        }

        [JsonRpcMethod("la_getNodeByHashBatch")]
        private JArray GetNodeByHashBatch(List<string> nodeHashList)
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
        private JObject GetChildrenByHash(string nodeHash)
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
        private JArray GetChildrenByHashBatch(List<string> nodeHashList)
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
        private string GetRootHashByTrieName(string trieName, string blockTag)
        {
            var blockNumber = GetBlockNumberByTag(blockTag);
            if (blockNumber == null) return "0x";
            IBlockchainSnapshot blockchainSnapshot = _snapshotIndexer.GetSnapshotForBlock((ulong)blockNumber);
            var snapshot = blockchainSnapshot.GetSnapshot(trieName);
            if (snapshot == null) return "0x";
            return Web3DataFormatUtils.Web3Data(snapshot.Hash);
        }

        [JsonRpcMethod("la_getNodeByVersion")]
        private JObject GetNodeByVersion(string versionTag)
        {
            var version = GetVersionNumberByTag(versionTag);
            if (version == null) return new JObject { };
            var node = _nodeRetrieval.TryGetNode((ulong)version);
            if (node == null) return new JObject { };
            return Web3DataFormatUtils.Web3Node(node);
        }
        [JsonRpcMethod("la_getChildrenByVersion")]
        private JObject GetChildrenByVersion(string versionTag)
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
        private JArray GetChildrenByVersionBatch(List<string> versionTags)
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
        private string GetRootVersionByTrieName(string trieName, string blockTag)
        {
            var blockNumber = GetBlockNumberByTag(blockTag);
            if (blockNumber == null) return "0x";
            IBlockchainSnapshot blockchainSnapshot = _snapshotIndexer.GetSnapshotForBlock((ulong)blockNumber);
            ISnapshot? snapshot =  blockchainSnapshot.GetSnapshot(trieName);
            if (snapshot is null) return "0x";
            else return Web3DataFormatUtils.Web3Number(snapshot.Version);
        }

        [JsonRpcMethod("eth_getBlockByHash")]
        private JObject? GetBlockByHash(string blockHash)
        {
            var block = _blockManager.GetByHash(blockHash.HexToBytes().ToUInt256());
            if (block == null)
                return null;
            var txs = block!.TransactionHashes
                .Select(hash => _transactionManager.GetByHash(hash)!)
                .ToList();
            var gasUsed = txs.Aggregate<TransactionReceipt, ulong>(0, (current, tx) => current + tx.GasUsed);
            var txArray = Web3DataFormatUtils.Web3BlockTransactionArray(txs, block!.Hash, block!.Header.Index);
            return Web3DataFormatUtils.Web3Block(block!, gasUsed, txArray);
        }

        [JsonRpcMethod("eth_getTransactionsByBlockHash")]
        private JObject? GetTransactionsByBlockHash(string blockHash)
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
        private string? GetBlockTransactionsCountByNumber(string blockTag)
        {
            var blockNumber = GetBlockNumberByTag(blockTag);
            if (blockNumber == null) 
                return Web3DataFormatUtils.Web3Number(_transactionPool.Size());
            var block = _blockManager.GetByHeight((ulong)blockNumber);
            return block == null ? null : Web3DataFormatUtils.Web3Number((ulong)block!.TransactionHashes.Count);
        }

        [JsonRpcMethod("eth_getBlockTransactionCountByHash")]
        private string? GetBlockTransactionsCountByHash(string blockHash)
        {
            var block = _blockManager.GetByHash(blockHash.HexToUInt256());
            return block == null ? null : Web3DataFormatUtils.Web3Number((ulong)block!.TransactionHashes.Count);
        }

        [JsonRpcMethod("eth_blockNumber")]
        private string GetBlockNumber()
        {
            return Web3DataFormatUtils.Web3Number(_blockManager.GetHeight());
        }

        [JsonRpcMethod("la_getDownloadedNodesTillNow")]
        private string GetDownloadedNodesTillNow()
        {
            return Web3DataFormatUtils.Web3Number(_nodeRetrieval.GetDownloadedNodeCount());
        }

        [JsonRpcMethod("la_validator_info")]
        private JObject GetValidatorInfo(string publicKeyStr)
        {
            var publicKey = publicKeyStr.HexToBytes();
            var addressUint160 = Crypto.ComputeAddress(publicKey).ToUInt160();

            var balance = _stateManager.CurrentSnapshot.Balances.GetBalance(addressUint160);

            var stake = _systemContractReader.GetStake(addressUint160).ToMoney().ToWei();
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
                ["address"] = addressUint160.ToString(),
                ["publicKey"] = publicKey,
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
        private JArray GetEventsByTransactionHash(string txHash)
        {
            var transactionHash = txHash.HexToUInt256();
            var txEvents = _stateManager.LastApprovedSnapshot.Events.GetTotalTransactionEvents(transactionHash);
            var jArray = new JArray();
            for (var i = 0; i < txEvents; i++)
            {
                var ev = _stateManager.LastApprovedSnapshot.Events.GetEventByTransactionHashAndIndex(transactionHash,
                    (uint)i);
                if (ev is null)
                    continue;
                jArray.Add(Web3DataFormatUtils.Web3Event(ev));
            }

            return jArray;
        }

        [JsonRpcMethod("eth_getLogs")]
        private JArray GetLogs(JObject opts)
        {
            var fromBlock = opts["fromBlock"];
            var toBlock = opts["toBlock"];
            var address = opts["address"];
            var topicsJson = opts["topics"];
            var blockhash = opts["blockHash"];

            if (!(fromBlock is null) && !(toBlock is null) && !(blockhash is null))
                throw new Exception(
                    "If blockHash is present in in the filter criteria, then neither fromBlock nor toBlock are allowed.");

            var start = (ulong)0;
            if (!(fromBlock is null))
            {
                if (((string)fromBlock!).StartsWith("0x"))
                {
                    start = UInt64.Parse(((string)fromBlock!).Substring(2), NumberStyles.HexNumber);
                }
                else
                {
                    start = (ulong)fromBlock!;
                }
            }

            var finish = _blockManager.GetHeight();
            if (!(toBlock is null))
            {
                if (((string)toBlock!).StartsWith("0x"))
                {
                    finish = UInt64.Parse(((string)toBlock!).Substring(2), NumberStyles.HexNumber);
                }
                else
                {
                    finish = (ulong)toBlock!;
                }
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
            
            var topics = new List<UInt256>();
            if (!(topicsJson is null))
            {
                foreach (var t_sig in topicsJson)
                {
                    if (t_sig is null)
                        break;
                    foreach (var t in t_sig)
                    {
                        var tString = (t is null) ? null : (string)t!;
                        var topicBuffer = tString?.HexToUInt256();
                        if (!(topicBuffer is null))
                        {
                            Logger.LogInformation($"Use topic [{topicBuffer.ToHex()}]");
                            topics.Add(topicBuffer);
                        }
                    }

                    break; // we check event signatures only,  no indexed parameters support
                    // TODO: Throw an error if there are topics for indexed parameters
                }
            }

            var addresses = new List<UInt160>();
            if (!(address is null))
            {
                foreach (var a in address)
                {
                    var addressString = (a is null) ? null : (string)a!;
                    var addressBuffer = addressString?.HexToUInt160();
                    if (!(addressBuffer is null))
                        addresses.Add(addressBuffer);
                }
            }

            var jArray = new JArray();
            for (var blockNumber = start; blockNumber <= finish; blockNumber++)
            {
                var block = _blockManager.GetByHeight((ulong)blockNumber);
                if (block == null)
                    continue;
                var txs = block!.TransactionHashes;
                foreach (var tx in txs)
                {
                    if (addresses.Count > 0)
                    {
                        var receipt = _stateManager.LastApprovedSnapshot.Transactions.GetTransactionByHash(tx);
                        if (receipt is null)
                            continue;
                        if (!addresses.Any(a => receipt.Transaction.From.Equals(a)))
                            continue;
                    }

                    var txEvents = _stateManager.LastApprovedSnapshot.Events.GetTotalTransactionEvents(tx);
                    for (var i = 0; i < txEvents; i++)
                    {
                        var ev = _stateManager.LastApprovedSnapshot.Events.GetEventByTransactionHashAndIndex(tx,
                            (uint)i);
                        if (ev is null)
                            continue;
                        if (topics.Count > 0)
                        {
                            if (!topics.Any(t => ev.SignatureHash.Equals(t)))
                            {
                                Logger.LogInformation($"Skip event with signature [{ev.SignatureHash.ToHex()}]");
                                continue;
                            }
                        }

                        if (ev.BlockHash is null)
                        {
                            ev.BlockHash = block.Hash;
                        }
                        if (ev.BlockHash.IsZero())
                        {
                            ev.BlockHash = block.Hash;
                        }
                        jArray.Add(Web3DataFormatUtils.Web3Event(ev, blockNumber));
                    }
                }
            }

            return jArray;
        }

        [JsonRpcMethod("eth_getTransactionPool")]
        private JArray GetTransactionPool()
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
        private string ChainId()
        {
            return TransactionUtils.ChainId.ToHex();
        }

        [JsonRpcMethod("net_version")]
        private string NetVersion()
        {
            return TransactionUtils.ChainId.ToHex();
        }


        [JsonRpcMethod("eth_getTransactionPoolByHash")]
        private JObject? GetTransactionPoolByHash(string txHash)
        {
            var transaction = _transactionPool.GetByHash(txHash.HexToUInt256());
            return transaction?.ToJson();
        }

        [JsonRpcMethod("eth_getStorageAt")]
        private string GetStorageAt(string address, string position, string blockTag)
        {
            // TODO: get data at given address and position, blockTag is the same as in eth_getBalance
            return Web3DataFormatUtils.Web3Data("".HexToBytes());
            //throw new ApplicationException("Not implemented");
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
    }
}