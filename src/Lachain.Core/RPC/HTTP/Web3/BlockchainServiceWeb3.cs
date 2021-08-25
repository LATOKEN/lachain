using System;
using System.Linq;
using AustinHarris.JsonRpc;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Pool;
using Lachain.Crypto;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Storage.Repositories;
using Lachain.Storage.State;
using Lachain.Utility.JSON;
using Lachain.Utility.Utils;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Globalization;
using NLog.Fluent;

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
        public BlockchainServiceWeb3(
            ITransactionManager transactionManager,
            IBlockManager blockManager,
            ITransactionPool transactionPool,
            IStateManager stateManager,
            ISnapshotIndexRepository snapshotIndexer)
        {
            _transactionPool = transactionPool;
            _transactionManager = transactionManager;
            _blockManager = blockManager;
            _stateManager = stateManager;
            _snapshotIndexer = snapshotIndexer;
        }

        [JsonRpcMethod("eth_getBlockByNumber")]
        private JObject? GetBlockByNumber(string blockTag, bool fullTx)
        {
            var blockNumber =GetBlockNumberByTag(blockTag);
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
            var blockNumber = GetBlockNumberByTag(blockTag) ;
            if(blockNumber == null) return null;
            IBlockchainSnapshot blockchainSnapshot = _snapshotIndexer.GetSnapshotForBlock((ulong)blockNumber);
            var jobject = new JObject{};
            jobject["Balances"] = Web3DataFormatUtils.Web3Trie(blockchainSnapshot.Balances.GetState());
            jobject["Contracts"] = Web3DataFormatUtils.Web3Trie(blockchainSnapshot.Contracts.GetState());
            jobject["Storage"] = Web3DataFormatUtils.Web3Trie(blockchainSnapshot.Storage.GetState()) ;
            jobject["Transactions"] = Web3DataFormatUtils.Web3Trie(blockchainSnapshot.Transactions.GetState());
            jobject["Blocks"] = Web3DataFormatUtils.Web3Trie(blockchainSnapshot.Blocks.GetState());
            jobject["Events"] = Web3DataFormatUtils.Web3Trie(blockchainSnapshot.Events.GetState()) ;
            jobject["Validators"] = Web3DataFormatUtils.Web3Trie(blockchainSnapshot.Validators.GetState());

            jobject["BalancesRoot"] = Web3DataFormatUtils.Web3Number(blockchainSnapshot.Balances.Version);
            jobject["ContractsRoot"] = Web3DataFormatUtils.Web3Number(blockchainSnapshot.Contracts.Version);
            jobject["StorageRoot"] = Web3DataFormatUtils.Web3Number(blockchainSnapshot.Storage.Version) ;
            jobject["TransactionsRoot"] = Web3DataFormatUtils.Web3Number(blockchainSnapshot.Transactions.Version);
            jobject["BlocksRoot"] = Web3DataFormatUtils.Web3Number(blockchainSnapshot.Blocks.Version);
            jobject["EventsRoot"] = Web3DataFormatUtils.Web3Number(blockchainSnapshot.Events.Version);
            jobject["ValidatorsRoot"] = Web3DataFormatUtils.Web3Number(blockchainSnapshot.Validators.Version);

            return jobject ;
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
            ulong l = startBlockTag.HexToUlong(), r = endBlockTag.HexToUlong();
            var jobject = new JObject{};
            for(ulong i=l; i<=r; i++){
                jobject[ i.ToHex(false) ] = Web3DataFormatUtils.Web3Data(SingleNodeHashFromRoot(i.ToHex(false)));
            }
            return jobject;
        }

        [JsonRpcMethod("la_getStateHashFromTrieRoots")]
        private string GetStateHashFromTrieRoots(string blockTag)
        {
            return Web3DataFormatUtils.Web3Data(SingleNodeHashFromRoot(blockTag)); 
        }

        [JsonRpcMethod("la_getAllTriesHash")]
        private JObject? GetAllTrieRootsHash(string blockTag)
        {
            var blockNumber = GetBlockNumberByTag(blockTag) ;
            IBlockchainSnapshot blockchainSnapshot = _snapshotIndexer.GetSnapshotForBlock((ulong)blockNumber);
            var jobject = new JObject{};
            jobject["BalancesHash"] = Web3DataFormatUtils.Web3Data(blockchainSnapshot.Balances.Hash);
            jobject["ContractsHash"] = Web3DataFormatUtils.Web3Data(blockchainSnapshot.Contracts.Hash);
            jobject["StorageHash"] = Web3DataFormatUtils.Web3Data(blockchainSnapshot.Storage.Hash) ;
            jobject["TransactionsHash"] = Web3DataFormatUtils.Web3Data(blockchainSnapshot.Transactions.Hash);
            jobject["BlocksHash"] = Web3DataFormatUtils.Web3Data(blockchainSnapshot.Blocks.Hash);
            jobject["EventsHash"] = Web3DataFormatUtils.Web3Data(blockchainSnapshot.Events.Hash) ;
            jobject["ValidatorsHash"] = Web3DataFormatUtils.Web3Data(blockchainSnapshot.Validators.Hash);
            return jobject ;
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
            var blockNumber = GetBlockNumberByTag(blockTag) ;
            IBlockchainSnapshot blockchainSnapshot = _snapshotIndexer.GetSnapshotForBlock((ulong)blockNumber);
            List<byte[]> list = new List<byte[]>();
            list.Add(blockchainSnapshot.Balances.Hash.ToBytes());
            list.Add(blockchainSnapshot.Contracts.Hash.ToBytes());
            list.Add(blockchainSnapshot.Storage.Hash.ToBytes());
            list.Add(blockchainSnapshot.Transactions.Hash.ToBytes());
            list.Add(blockchainSnapshot.Events.Hash.ToBytes());
            list.Add(blockchainSnapshot.Validators.Hash.ToBytes());
            return list.Flatten().Keccak(); 
        }

    }
}