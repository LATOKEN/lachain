using Newtonsoft.Json.Linq;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Utility.JSON
{
    public static class EventJsonConverter
    {
        public static JObject ToJson(this Event block)
        {
            var json = new JObject
            {
                ["contract"] = block.Contract.ToHex(),
                ["data"] = block.Data.ToHex(),
                ["transactionHash"] = block.TransactionHash.ToHex(),
                ["index"] = block.Index,
                ["signatureHash"] = block.SignatureHash.ToHex()
            };
            return json;
        }
    }
}