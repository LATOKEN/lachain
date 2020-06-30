using System;
using System.Linq;
using AustinHarris.JsonRpc;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Pool;
using Lachain.Logger;
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
            var height = blockHeight.HexToUlong();
            var block = _blockManager.GetByHeight(height) ?? throw new InvalidOperationException("Block not found");
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
                            nativeTx, block.Hash.ToHex(), block.Header.Index.ToHex())
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
                ["mixHash"] = "0x0000000000000000000000000000000000000000000000000000000000000000",
                ["nonce"] = block.Header.Nonce.ToHex(),
                ["sha3Uncles"] = "0x1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347",
                ["logsBloom"] =
                    "0x00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000",
                ["transactionsRoot"] = block.Header.MerkleRoot.ToHex(),
                ["stateRoot"] = block.Header.StateHash.ToHex(),
                ["receiptsRoot"] = "0x056b23fbba480696b65fe5a59b8f2148a1299103c4f57df839233af2cf4ca2d2",
                ["miner"] = "0x0300000000000000000000000000000000000000",
                ["difficulty"] = "0x0",
                ["totalDifficulty"] = "0x0",
                ["extraData"] = "0x",
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
        private string? GetBlockTransactionsCountByNumber(string blockHeight)
        {
            var number = blockHeight.HexToUlong();
            try
            {
                var block = _blockManager.GetByHeight(number) ?? throw new Exception();
                var count = block.TransactionHashes.Count;
                return $"0x{count:X}";
            }
            catch (Exception)
            {
                return null;
            }
        }

        [JsonRpcMethod("eth_blockNumber")]
        private string GetBlockNumber()
        {
            return $"0x{_blockManager.GetHeight():X}";
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


        [JsonRpcMethod("eth_getTransactionPoolByHash")]
        private JObject? GetTransactionPoolByHash(string txHash)
        {
            var transaction = _transactionPool.GetByHash(txHash.HexToUInt256());
            return transaction?.ToJson();
        }
    }
}