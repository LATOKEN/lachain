using AustinHarris.JsonRpc;
using Newtonsoft.Json.Linq;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.JSON;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.RPC.HTTP
{
    public class BlockchainService : JsonRpcService
    {
        private readonly ITransactionManager _transactionManager;
        private readonly IBlockManager _blockManager;
        private readonly IBlockchainContext _blockchainContext;

        public BlockchainService(
            ITransactionManager transactionManager,
            IBlockManager blockManager,
            IBlockchainContext blockchainContext)
        {
            _transactionManager = transactionManager;
            _blockManager = blockManager;
            _blockchainContext = blockchainContext;
        }

        [JsonRpcMethod("getBlockByHeight")]
        private JObject GetBlockByHeight(uint blockHeight)
        {
            var block = _blockManager.GetByHeight(blockHeight);
            return block?.ToJson();
        }

        [JsonRpcMethod("getBlockByHash")]
        private JObject GetBlockByHash(string blockHash)
        {
            var block = _blockManager.GetByHash(blockHash.HexToBytes().ToUInt256());
            return block?.ToJson();
        }

        [JsonRpcMethod("getTransactionByHash")]
        private JObject GetTransactionByHash(string txHash)
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
    }
}