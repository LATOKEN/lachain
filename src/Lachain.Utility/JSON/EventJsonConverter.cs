using Newtonsoft.Json.Linq;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Utility.JSON
{
    public static class EventJsonConverter
    {
        public static JObject ToJson(this EventObject evObj)
        {
            var ev = evObj._event;
            var json = new JObject
            {
                ["contract"] = ev!.Contract.ToHex(),
                ["data"] = ev.Data.ToHex(),
                ["transactionHash"] = ev.TransactionHash.ToHex(),
                ["index"] = ev.Index,
                ["signatureHash"] = ev.SignatureHash.ToHex()
            };
            return json;
        }
    }
}