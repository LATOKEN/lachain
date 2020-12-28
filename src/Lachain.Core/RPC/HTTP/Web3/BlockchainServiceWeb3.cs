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
        private JObject? GetBlockByNumber(string blockHeight, bool fullTx)
        {
            var height = blockHeight == "latest" 
                ? _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight()
                : blockHeight.HexToUlong();
            
            var block = _blockManager.GetByHeight(height) ?? throw new InvalidOperationException("Block not found");

            string parentHash = "0x0000000000000000000000000000000000000000000000000000000000000000";
            if (height > 0)
            {
                parentHash = _blockManager.GetByHeight(height - 1)!.Hash.ToHex();
            }
            ulong gasUsed = 0;
            JArray txs;
            {
                txs = new JArray();
                foreach (var txHash in block.TransactionHashes)
                {
                    var nativeTx = _transactionManager.GetByHash(txHash);
                    if (nativeTx is null)
                    {
                        Logger.LogWarning($"Block {height} has tx {txHash.ToHex()}, but there is no such tx");
                        continue;
                    }

                    gasUsed += nativeTx.GasUsed;
                    if (fullTx)
                    {
                        txs.Add(TransactionServiceWeb3.ToEthTxFormat(
                            _stateManager,
                            nativeTx, 
                            block.Hash.ToHex(), 
                            block.Header.Index.ToHex()
                            )
                        );
                    }
                }
            }
            if (!fullTx)
            {
                txs = new JArray(block.TransactionHashes.Select(txHash => txHash.ToHex()));
            }

            return new JObject
            {
                ["author"] = "0x0300000000000000000000000000000000000000",
                ["number"] = block.Header.Index.ToHex(),
                ["hash"] = block.Hash.ToHex(),
                ["parentHash"] = parentHash,
                // ["mixHash"] = "0x0000000000000000000000000000000000000000000000000000000000000000",
                ["nonce"] = block.Header.Nonce,
                ["sha3Uncles"] = "0x1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347",
                ["logsBloom"] =
                    "0x00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000",
                ["transactionsRoot"] = block.Header.MerkleRoot.ToHex(),
                ["stateRoot"] = block.Header.StateHash.ToHex(),
                ["receiptsRoot"] = "0x056b23fbba480696b65fe5a59b8f2148a1299103c4f57df839233af2cf4ca2d2",
                ["miner"] = "0x0300000000000000000000000000000000000000",
                ["difficulty"] = "0x0",
                ["totalDifficulty"] = "0x0",
                ["extraData"] = "0x0",
                ["size"] = block.CalculateSize().ToHex(),
                ["gasLimit"] = "0x174876e800",
                ["gasUsed"] = gasUsed.ToHex(),
                ["timestamp"] = (block.Timestamp / 1000).ToHex(),
                ["transactions"] = txs,
                ["uncles"] = new JArray()
            };
        }

        [JsonRpcMethod("eth_getBlockByHash")]
        private JObject? GetBlockByHash(string blockHash)
        {
            var block = _blockManager.GetByHash(blockHash.HexToBytes().ToUInt256());
            return block?.ToJson();
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