using System.Linq;
using AustinHarris.JsonRpc;
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
    public class BlockchainService : JsonRpcService
    {
        private readonly ITransactionManager _transactionManager;
        private readonly IBlockManager _blockManager;
        private readonly IBlockchainContext _blockchainContext;
        private readonly IStateManager _stateManager;
        private readonly ITransactionPool _transactionPool;

        public BlockchainService(
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

        [JsonRpcMethod("getBlockByHeight")]
        private JObject? GetBlockByHeight(uint blockHeight)
        {
            var block = _blockManager.GetByHeight(blockHeight);
            return block?.ToJson();
        }

        [JsonRpcMethod("getBlockByHash")]
        private JObject? GetBlockByHash(string blockHash)
        {
            var block = _blockManager.GetByHash(blockHash.HexToBytes().ToUInt256());
            return block?.ToJson();
        }
        
        [JsonRpcMethod("getTransactionsByBlockHash")]
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

        [JsonRpcMethod("getTransactionByHash")]
        private JObject? GetTransactionByHash(string txHash)
        {
            var tx = _transactionManager.GetByHash(txHash.HexToBytes().ToUInt256());
            return tx?.ToJson();
        }

        [JsonRpcMethod("getBlockStat")]
        private JObject GetBlockStat()
        {
            var json = new JObject
            {
                ["currentHeight"] = _blockchainContext.CurrentBlockHeight
            };
            return json;
        }

        [JsonRpcMethod("getEventsByTransactionHash")]
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
        
        [JsonRpcMethod("getTransactionPool")]
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
        
        
        [JsonRpcMethod("getTransactionPoolByHash")]
        private JObject? GetTransactionPoolByHash(string txHash)
        {
            var transaction = _transactionPool.GetByHash(HexUtils.HexToUInt256(txHash));
            return transaction?.ToJson();
        }
    }
}