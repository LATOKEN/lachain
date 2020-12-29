using System;
using System.Linq;
using AustinHarris.JsonRpc;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Pool;
using Lachain.Crypto;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Utility.JSON;
using Lachain.Utility.Utils;
using Newtonsoft.Json.Linq;

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

        public BlockchainServiceWeb3(
            ITransactionManager transactionManager,
            IBlockManager blockManager,
            ITransactionPool transactionPool,
            IStateManager stateManager)
        {
            _transactionPool = transactionPool;
            _transactionManager = transactionManager;
            _blockManager = blockManager;
            _stateManager = stateManager;
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
            var block = _blockManager.GetByHash(blockHash.HexToBytes().ToUInt256()) ??
                        throw new Exception($"No block with hash {blockHash}");
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
                jArray.Add(ev.ToJson());
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
            throw new ApplicationException("Not implemented yet");
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
    }
}