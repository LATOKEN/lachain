using Newtonsoft.Json.Linq;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Utility.JSON
{
    public static class TransactionJsonConverter
    {
        public static JObject ToJson(this Transaction transaction)
        {
            var json = new JObject
            {
                ["type"] = transaction.Type.ToString(),
                ["to"] = transaction.To.ToHex(),
                ["invocation"] = transaction.Invocation.ToHex(),
                ["value"] = transaction.Value.ToHex(),
                ["from"] = transaction.From.ToHex(),
                ["nonce"] = transaction.Nonce,
                ["gasLimit"] = transaction.GasLimit,
                ["gasPrice"] = transaction.GasPrice,
            };
            return json;
        }
        
        public static JObject ToJson(this TransactionReceipt acceptedTransaction)
        {
            var json = new JObject
            {
                ["transaction"] = acceptedTransaction.Transaction.ToJson(),
                ["hash"] = acceptedTransaction.Hash.ToHex(),
                ["signature"] = acceptedTransaction.Signature.ToHex(),
                ["block"] = acceptedTransaction.Block,
                ["status"] = acceptedTransaction.Status.ToString()
            };
            return json;
        }
    }
}