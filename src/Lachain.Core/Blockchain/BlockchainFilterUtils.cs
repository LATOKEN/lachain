using System.Collections.Generic;
using Lachain.Utility.Utils;
using Lachain.Proto;
using Newtonsoft.Json.Linq;
using System;

namespace Lachain.Core.BlockchainFilter{

    public static class BlockchainFilterUtils
    {
        public static List<UInt256> GetTopics(JToken topicsJson)
        {
            // We handle only first topic for now. Change the implementation when other topics are supported
            var topics = new List<UInt256>();
            try{
                var topicArray = (JArray)topicsJson;
                foreach(var topic in topicArray)
                {

                    try{
                        JArray topicList = (JArray)topic;
                        foreach (var t in topicList)
                        {
                            var topicString = (string)t!;
                            var topicBuffer = topicString.HexToUInt256();
                            topics.Add(topicBuffer);        
                        }
                    }
                    catch(InvalidCastException _){
                        
                        var topicString = (string)topic!;
                        if(!(topicString is null)){
                            var topicBuffer = topicString.HexToUInt256();
                            topics.Add(topicBuffer);
                        }
                    
                    }
                    break; // Only first topic is supported now.
                    // TODO: change implementation when other topics are supported
                }

            }
            catch(InvalidCastException _){
                var topicString = (string)topicsJson!;
                if(!(topicString is null)){
                    var topicBuffer = topicString.HexToUInt256();
                    topics.Add(topicBuffer);
                }
            }
            return topics;
        }

        public static List<UInt160> GetAddresses(JArray address){
            List<UInt160> addresses = new  List<UInt160>();
            foreach (var a in address)
            {
                var addressString = (string)a!;
                if (!(addressString is null)){
                    var addressBuffer = addressString.HexToUInt160();
                    addresses.Add(addressBuffer);
                }
            }
            return addresses;
        }
    }
}
