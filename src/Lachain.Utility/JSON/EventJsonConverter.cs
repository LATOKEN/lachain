using System;
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
            if(ev is null) throw new Exception("event is null");
            var topics = new JArray();
            topics.Add(ev.SignatureHash.ToHex());
            var topicList = evObj._topics;
            if(topicList != null)
            {
                foreach(var topic in topicList)
                {
                    topics.Add(topic.ToHex());
                }
            }
            var json = new JObject
            {
                ["contract"] = ev!.Contract.ToHex(),
                ["data"] = ev.Data.ToHex(),
                ["transactionHash"] = ev.TransactionHash.ToHex(),
                ["index"] = ev.Index,
                ["topics"] = topics
            };
            return json;
        }
    }
}