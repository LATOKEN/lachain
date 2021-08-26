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
using Lachain.Storage.Trie;


namespace Lachain.Core.RPC.HTTP.Web3
{
    public class BlockchainServiceWeb3 : JsonRpcService
    {
        private static readonly ILogger<BlockchainServiceWeb3> Logger =
            LoggerFactory.GetLoggerForClass<BlockchainServiceWeb3>();

        private readonly ITransactionManager _transactionManager;
        private readonly IBlockManager _blockManager;
        private readonly IStateManager _stateManager;
        private readonly ITransactionPool _transactionPool; 
        private readonly ISnapshotIndexRepository _snapshotIndexer;
        private readonly INetworkManager _networkManager;
        private readonly INodeRetrieval _nodeRetrieval; 
        public BlockchainServiceWeb3(
            ITransactionManager transactionManager,
            IBlockManager blockManager,
            ITransactionPool transactionPool,
            IStateManager stateManager,
            ISnapshotIndexRepository snapshotIndexer,
            INetworkManager networkManager,
            INodeRetrieval nodeRetrieval)
        {
            _transactionPool = transactionPool;
            _transactionManager = transactionManager;
            _blockManager = blockManager;
            _stateManager = stateManager;
            _snapshotIndexer = snapshotIndexer;
            _networkManager = networkManager;
            _nodeRetrieval = nodeRetrieval;
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
            var txArray = fullTx ? Web3DataFormatUtils.Web3BlockTransactionArray(txs, block!.Hash, block!.Header.Index) : new JArray();
            return Web3DataFormatUtils.Web3Block(block!, gasUsed, txArray);
        }

        [JsonRpcMethod("la_getStateByNumber")]
        private JObject? GetStateByNumber(string blockTag)
        {
            var blockNumber = GetBlockNumberByTag(blockTag);
            if(blockNumber == null) return null;
            IBlockchainSnapshot blockchainSnapshot = _snapshotIndexer.GetSnapshotForBlock((ulong)blockNumber);
            var state = new JObject{};

            string[] trieNames = new string[] { "Balances", "Contracts", "Storage", "Transactions", "Blocks", "Events", "Validators" };
            ISnapshot[] snapshots = new ISnapshot[]{blockchainSnapshot.Balances,
                                                    blockchainSnapshot.Contracts,
                                                    blockchainSnapshot.Storage,
                                                    blockchainSnapshot.Transactions,
                                                    blockchainSnapshot.Blocks,
                                                    blockchainSnapshot.Events,
                                                    blockchainSnapshot.Validators};

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
            res &= blockchainSnapshot.Balances.IsTrieNodeHashesOk();
            res &= blockchainSnapshot.Contracts.IsTrieNodeHashesOk();
            res &= blockchainSnapshot.Storage.IsTrieNodeHashesOk();
            res &= blockchainSnapshot.Transactions.IsTrieNodeHashesOk();
            res &= blockchainSnapshot.Blocks.IsTrieNodeHashesOk();
            res &= blockchainSnapshot.Events.IsTrieNodeHashesOk();
            res &= blockchainSnapshot.Validators.IsTrieNodeHashesOk();
            return Web3DataFormatUtils.Web3Number(Convert.ToUInt64(res));
        }

        [JsonRpcMethod("la_getStateHashFromTrieRootsRange")]
        private JObject? GetStateHashFromTrieRootsRange(string startBlockTag, string endBlockTag)
        {
            ulong startBlock = startBlockTag.HexToUlong(), endBlock = endBlockTag.HexToUlong();
            var stateHash = new JObject{};
            for(ulong curBlock = startBlock; curBlock <= endBlock; curBlock++) {
                stateHash[curBlock.ToHex(false)] = Web3DataFormatUtils.Web3Data(SingleNodeHashFromRoot(curBlock.ToHex(false)));
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
            trieRootsHash["BalancesHash"] = Web3DataFormatUtils.Web3Data(blockchainSnapshot.Balances.Hash);
            trieRootsHash["ContractsHash"] = Web3DataFormatUtils.Web3Data(blockchainSnapshot.Contracts.Hash);
            trieRootsHash["StorageHash"] = Web3DataFormatUtils.Web3Data(blockchainSnapshot.Storage.Hash);
            trieRootsHash["TransactionsHash"] = Web3DataFormatUtils.Web3Data(blockchainSnapshot.Transactions.Hash);
            trieRootsHash["BlocksHash"] = Web3DataFormatUtils.Web3Data(blockchainSnapshot.Blocks.Hash);
            trieRootsHash["EventsHash"] = Web3DataFormatUtils.Web3Data(blockchainSnapshot.Events.Hash);
            trieRootsHash["ValidatorsHash"] = Web3DataFormatUtils.Web3Data(blockchainSnapshot.Validators.Hash);
            return trieRootsHash;
        }


        [JsonRpcMethod("la_getNodeByVersion")]
        private JObject? GetNodeByVersion(string versionTag)
        {
            var version = GetBlockNumberByTag(versionTag);
            if (version == null || version == 0) return null;
            // to do : handle if the database does not have the node
            return Web3DataFormatUtils.Web3Node(_nodeRetrieval.TryGetNode((ulong)version));
        }

        [JsonRpcMethod("la_getChildByVersion")]
        private JObject? GetChildByVersion(string versionTag)
        {
            var version = GetBlockNumberByTag(versionTag);
            JObject children = new JObject {};
            if (version == null || version == 0) return new JObject { };
            var node = _nodeRetrieval.TryGetNode((ulong)version);
            switch (node)
            {
                case InternalNode internalNode:
                    foreach (var item in node.Children) children[Web3DataFormatUtils.Web3Number(item)] =
                            Web3DataFormatUtils.Web3Node(_nodeRetrieval.TryGetNode(item));
                    
                    return children;

                case LeafNode leafNode:
                    return children;
            }
            return children;
        }

        [JsonRpcMethod("la_getChildByVersionBatch")]
        private JObject? GetChildByVersionBatch(List<string> versionTags)
        {
            JObject childsBatch = new JObject { };
            foreach (var versionTag in versionTags)
            {
                JObject childs = new JObject { };
                var version = GetBlockNumberByTag(versionTag);
                if (version == null || version == 0) return new JObject { };
                var node = _nodeRetrieval.TryGetNode((ulong)version);
                switch (node)
                {
                    case InternalNode internalNode:
                        foreach (var item in node.Children) childs[Web3DataFormatUtils.Web3Number(item)] =
                                Web3DataFormatUtils.Web3Node(_nodeRetrieval.TryGetNode(item));
                        break;
                    case LeafNode leafNode:
                        return childs;
                }
                childsBatch[Web3DataFormatUtils.Web3Number((ulong)version)] = childs;
            }
            return childsBatch;
        }


        [JsonRpcMethod("la_getRootVersionByTrieName")]
        private string? GetRootVersionByTrieName(string trieName, string blockTag)
        {
            var blockNumber = GetBlockNumberByTag(blockTag);
            if (blockNumber == null) return null;
            IBlockchainSnapshot blockchainSnapshot = _snapshotIndexer.GetSnapshotForBlock((ulong)blockNumber);
            string[] trieNames = new string[] { "Balances", "Contracts", "Storage", "Transactions", "Blocks", "Events", "Validators" };
            ISnapshot[] snapshots = new ISnapshot[]{blockchainSnapshot.Balances,
                                                    blockchainSnapshot.Contracts,
                                                    blockchainSnapshot.Storage,
                                                    blockchainSnapshot.Transactions,
                                                    blockchainSnapshot.Blocks,
                                                    blockchainSnapshot.Events,
                                                    blockchainSnapshot.Validators};

            for (var i = 0; i < trieNames.Length; i++)
            {
                if(trieNames[i] == trieName)
                {
                    return Web3DataFormatUtils.Web3Number(snapshots[i].Version);
                }
            }
            return null;
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
                return new JObject{["transactions"] = new JArray()};
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
            var blockNumber =GetBlockNumberByTag(blockTag);
            if (blockNumber == null) 
                return Web3DataFormatUtils.Web3Number(_transactionPool.Size());
            var block = _blockManager.GetByHeight((ulong)blockNumber);
            return block == null ? null : Web3DataFormatUtils.Web3Number((ulong) block!.TransactionHashes.Count);
        }

        [JsonRpcMethod("eth_getBlockTransactionCountByHash")]
        private string? GetBlockTransactionsCountByHash(string blockHash)
        {
            var block = _blockManager.GetByHash(blockHash.HexToUInt256());
            return block == null ? null : Web3DataFormatUtils.Web3Number((ulong) block!.TransactionHashes.Count);
        }

        [JsonRpcMethod("eth_blockNumber")]
        private string GetBlockNumber()
        {
            return Web3DataFormatUtils.Web3Number(_blockManager.GetHeight());
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
        private JObject? GetUncleByBlockHashAndIndex(string blockHash,  ulong index)
        {
            return null;
        }

        [JsonRpcMethod("eth_getUncleByBlockNumberAndIndex")]
        private JObject? GetUncleByBlockNumberAndIndex(string blockTag,  ulong index)
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
                    (uint) i);
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
            var topics = opts["topics"];
            var blockhash = opts["blockHash"];
            if (!(topics is null))
            {
                if(!((string)topics!).ToLower().Equals("null"))
                    throw new Exception("Topics filter is not implemented yet");
            }

            if (!(fromBlock is null) && !(toBlock is null) && !(blockhash is null))
                throw new Exception("If blockHash is present in in the filter criteria, then neither fromBlock nor toBlock are allowed.");
            
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
            for(var blockNumber = start; blockNumber <= finish; blockNumber++)
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
                            (uint) i);
                        if (ev is null)
                            continue;
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

        private UInt256 SingleNodeHashFromRoot(string blockTag)
        {
            var blockNumber = GetBlockNumberByTag(blockTag);
            IBlockchainSnapshot blockchainSnapshot = _snapshotIndexer.GetSnapshotForBlock((ulong)blockNumber);
            return blockchainSnapshot.StateHash;
        }
    }
}