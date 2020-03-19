using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AustinHarris.JsonRpc;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Newtonsoft.Json.Linq;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Blockchain.Interface;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Blockchain.Pool;
using Phorkus.Storage.State;
using Phorkus.Utility.JSON;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.RPC.HTTP
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
            var web3Block = new JObject
            {
            };
            web3Block["number"] = block.Header.Index.ToBytes().ToHex();
            web3Block["hash"] = block.Hash.Buffer.ToHex();
            // web3Block["parentHash"] = block..Buffer.ToHex(); // prev
            web3Block["mixHash"] = "0x0000000000000000000000000000000000000000000000000000000000000000";
            web3Block["nonce"] = "0x0000000000000000"; // hz
            web3Block["sha3Uncles"] = "0x1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347";
            web3Block["logsBloom"] = "0x00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000";
            web3Block["transactionsRoot"] = "0xe61a6e2a6b261aec50ff7ea7ff7f6495a08fa1aeacc42f30c354a4b628670469";
            web3Block["stateRoot"] = "0xe7f259cb4a031a43029f8466ba700c2aa9213f2693232e6d82bed7ca3c33f499";
            web3Block["receiptsRoot"] = "0x056b23fbba480696b65fe5a59b8f2148a1299103c4f57df839233af2cf4ca2d2";
            web3Block["miner"] = "0x0000000000000000000000000000000000000000";
            web3Block["difficulty"] = "0x0";
            web3Block["totalDifficulty"] = "0x0";
            web3Block["extraData"] = "0x";
            web3Block["size"] = block.CalculateSize().ToHexBigInteger().HexValue;
            web3Block["gasLimit"] = "0x5208";
            web3Block["gasUsed"] = "0x5208"; // hz
            web3Block["timestamp"] = (block.Timestamp / 1000).ToHexBigInteger().HexValue;
            web3Block["transactions"] = new JArray();
            web3Block["uncles"] = new JArray();
            
            return web3Block;
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
                Console.WriteLine("OK");
                Console.WriteLine(number);
                return $"0x{count:X}";
            }
            catch (Exception e)
            {
                Console.WriteLine("Error");
                Console.WriteLine(number);
                return null;
            }
        }

        [JsonRpcMethod("eth_getTransactionByHash")]
        private JObject? GetTransactionByHash(string txHash)
        {
            var tx = _transactionManager.GetByHash(txHash.HexToBytes().ToUInt256());
            return tx?.ToJson();
        }

        [JsonRpcMethod("net_version")]
        private string NetVersion()
        {
            return "1717";
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