using Newtonsoft.Json.Linq;
using Phorkus.Proto;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.JSON
{
    public static class TransactionJsonConverter
    {
        public static JObject ToJson(this Transaction transaction)
        {
            var json = new JObject
            {
                ["type"] = transaction.Type.ToString(),
                ["to"] = transaction.To.ToString(),
                ["invocation"] = transaction.Invocation?.ToHex(),
                ["value"] = transaction.Value.Buffer.ToHex(),
                ["from"] = transaction.From.Buffer?.ToHex(),
                ["nonce"] = transaction.Nonce,
                ["fee"] = transaction.Fee?.Buffer?.ToHex()
            };
            return json;
        }
        
        public static JObject ToJson(this AcceptedTransaction acceptedTransaction)
        {
            var json = new JObject
            {
                ["transaction"] = acceptedTransaction.Transaction?.ToJson(),
                ["hash"] = acceptedTransaction.Hash?.Buffer?.ToHex(),
                ["signature"] = acceptedTransaction.Signature?.Buffer?.ToHex(),
                ["block"] = acceptedTransaction.Block?.Buffer?.ToHex(),
                ["status"] = acceptedTransaction.Status.ToString()
            };
            return json;
        }
    }
}