using System.Linq;
using Newtonsoft.Json.Linq;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Utility.JSON
{
    public static class BlockJsonConverter
    {
        public static JObject ToJson(this BlockHeader blockHeader)
        {
            var json = new JObject
            {
                ["prevBlockHash"] = blockHeader.PrevBlockHash.ToHex(),
                ["merkleRoot"] = blockHeader.MerkleRoot.ToHex(),
                ["stateHash"] = blockHeader.StateHash.ToHex(),
                ["index"] = blockHeader.Index,
                ["nonce"] = blockHeader.Index
            };
            return json;
        }

        public static JObject ToJson(this Block block)
        {
            var json = new JObject
            {
                ["header"] = block.Header.ToJson(),
                ["hash"] = block.Hash.ToHex(),
                ["transactionHashes"] = new JArray(block.TransactionHashes.Select(txHash => txHash.ToHex())),
                ["multisig"] = null,
                ["gasPrice"] = block.GasPrice,
                ["timestamp"] = block.Timestamp,
            };
            return json;
        }
    }
}