using System.Collections.Generic;
using Lachain.Utility.Utils;
using Lachain.Proto;
using Newtonsoft.Json.Linq;
using System;

namespace Lachain.Core.BlockchainFilter{

    public static class BlockchainFilterUtils
    {

        public static List<List<UInt256>> GetTopics(JToken topicsJson)
        {
            var allTopics = new List<List<UInt256>>();
            try{
                var topicArray = (JArray)topicsJson;
                foreach(var topic in topicArray)
                {
                    var topics = new List<UInt256>();
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
                    allTopics.Add(topics);
                }

            }
            catch(InvalidCastException _){
                var topics = new List<UInt256>();
                var topicString = (string)topicsJson!;
                if(!(topicString is null)){
                    var topicBuffer = topicString.HexToUInt256();
                    topics.Add(topicBuffer);
                }
                allTopics.Add(topics);
            }
            while(allTopics.Count < 4) allTopics.Add(new List<UInt256>());

            Console.WriteLine("printing topics");
            for (int i = 0; i < allTopics.Count; i++){
                Console.WriteLine($"topic: {i + 1} of size: {allTopics[i].Count}");
                foreach(var topic in allTopics[i]){
                    Console.WriteLine(topic.ToHex());
                }
            }

            return allTopics;
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
