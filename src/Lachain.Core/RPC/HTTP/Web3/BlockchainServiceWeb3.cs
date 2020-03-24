using System;
using System.Linq;
using AustinHarris.JsonRpc;
using Nethereum.Hex.HexConvertors.Extensions;
using Newtonsoft.Json.Linq;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.OperationManager;
using Lachain.Core.Blockchain.Pool;
using Lachain.Storage.State;
using Lachain.Utility.JSON;
using Lachain.Utility.Utils;
using Lachain.Core.RPC.HTTP.Web3;

namespace Lachain.Core.RPC.HTTP
{
    public class BlockchainServiceWeb3 : JsonRpcService
    {
        private readonly ITransactionManager _transactionManager;
        private readonly IBlockManager _blockManager;
        private readonly IBlockchainContext _blockchainContext;
        private readonly IStateManager _stateManager;
        private readonly ITransactionPool _transactionPool;

        public BlockchainServiceWeb3(
            ITransactionManager transactionManager,
            IBlockManager blockManager,
            IBlockchainContext blockchainContext,
            ITransactionPool transactionPool,
            IStateManager stateManager)
        {
            _transactionPool = transactionPool;
            _transactionManager = transactionManager;
            _blockManager = blockManager;
            _blockchainContext = blockchainContext;
            _stateManager = stateManager;
        }

        [JsonRpcMethod("eth_getBlockByNumber")]
        private JObject? GetBlockByNumber(string blockHeight, bool fullTx)
        {
            var height = blockHeight.HexToUlong();
            var block = _blockManager.GetByHeight(height);
            ulong gasUsed = 0;
            JArray txs;
            {
                txs = new JArray();
                foreach (var txHash in block.TransactionHashes)
                {
                    var nativeTx = _transactionManager.GetByHash(txHash);
                    gasUsed += nativeTx.GasUsed;   
                    if (fullTx)
                    {
                        txs.Add(TransactionServiceWeb3.ToEthTxFormat(nativeTx, block.Hash.Buffer.ToByteArray().ToHex(true), block.Header.Index.ToHex()));
                    }
                }
            }
            if (!fullTx)
            {
                txs = new JArray(block.TransactionHashes.Select(txHash => txHash.Buffer.ToHex()));
            }
            return new JObject
            {
                ["number"] = block.Header.Index.ToHex(),
                ["hash"] = block.Hash.Buffer.ToHex(),
                ["mixHash"] = "0x0000000000000000000000000000000000000000000000000000000000000000",
                ["nonce"] = block.Header.Nonce.ToHex(),
                ["sha3Uncles"] = "0x1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347",
                ["logsBloom"] =
                    "0x00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000",
                ["transactionsRoot"] = block.Header.MerkleRoot.Buffer.ToHex(),
                ["stateRoot"] = block.Header.StateHash.Buffer.ToHex(),
                ["receiptsRoot"] = "0x056b23fbba480696b65fe5a59b8f2148a1299103c4f57df839233af2cf4ca2d2",
                ["miner"] = "0x0000000000000000000000000000000000000000",
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
            var block = _blockManager.GetByHash(blockHash.HexToBytes().ToUInt256());
            var txs = block.TransactionHashes
                .Select(hash => _transactionManager.GetByHash(hash)?.ToJson())
                .ToList();
            return new JObject
            {
                ["transactions"] = new JArray(txs),
            };
        }
        
        [JsonRpcMethod("eth_getBlockTransactionCountByNumber")]
        private string GetBlockTransactionsCountByNumber(string blockHeight)
        {
            var number = blockHeight.HexToUlong();
            try
            {
                var block = _blockManager.GetByHeight(number);
                var count = block.TransactionHashes.Count;
                return $"0x{count:X}";
            }
            catch (Exception e)
            {
                return null;
            }
        }

        [JsonRpcMethod("net_version")]
        private string NetVersion()
        {
            return "1";
        }

        [JsonRpcMethod("eth_blockNumber")]
        private string GetBlockNumber()
        {
            return $"0x{_blockchainContext.CurrentBlockHeight:X}";
        }

        [JsonRpcMethod("eth_getEventsByTransactionHash")]
        private JArray GetEventsByTransactionHash(string txHash)
        {
            var transactionHash = txHash.HexToUInt256();
            var txEvents = _stateManager.LastApprovedSnapshot.Events.GetTotalTransactionEvents(transactionHash);
            var jArray = new JArray();
            for (var i = 0; i < txEvents; i++)
            {
                var ev = _stateManager.LastApprovedSnapshot.Events.GetEventByTransactionHashAndIndex(transactionHash, (uint) i);
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