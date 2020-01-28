using Newtonsoft.Json.Linq;
using Phorkus.Proto;
using Phorkus.Utility.Utils;

namespace Phorkus.Utility.JSON
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