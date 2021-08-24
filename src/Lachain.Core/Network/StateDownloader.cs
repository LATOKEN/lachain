using System;
using System.Collections.Generic;
using System.Text;
using Lachain.Storage.Trie;
using Lachain.Core.RPC.HTTP.Web3;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Net;
using Lachain.Logger;


namespace Lachain.Core.Network
{
    public class StateDownloader
    {
        

        private static readonly ILogger<StateDownloader> Logger = LoggerFactory.GetLoggerForClass<StateDownloader>();

        private IDictionary<string, IDictionary<ulong, IHashTrieNode>> _trieNodes = new Dictionary<string, IDictionary<ulong, IHashTrieNode>>();
        private IDictionary<string, ulong> _trieRootVersions = new Dictionary<string, ulong>();
        private ulong _blockNumber;
        private string _peerURL;

        public StateDownloader(string peerURL)
        {
            Logger.LogWarning($"Starting to download state from {peerURL}");
            _peerURL = peerURL;
            _blockNumber = Convert.ToUInt64((string)_CallJsonRPCAPI("eth_blockNumber", new JArray {}, peerURL), 16);
            JObject? receivedInfo = (JObject?)_CallJsonRPCAPI("la_getStateByNumber", new JArray {Web3DataFormatUtils.Web3Number(_blockNumber)}, peerURL);
            string[] trieNames = new string[] {"Balances", "Contracts", "Storage", "Transactions", "Blocks", "Events", "Validators"};
            
            foreach(var trieName in trieNames)
            {
                JObject currentTrie = (JObject)receivedInfo[trieName];
                string currentTrieRoot = (string)receivedInfo[trieName + "Root"];
                _trieRootVersions[trieName] = Convert.ToUInt64(currentTrieRoot, 16);
                _trieNodes[trieName] = Web3DataFormatUtils.TrieFromJson(currentTrie);
            }

            Logger.LogWarning($"Completed downloading state from {peerURL}");
        }

        public ulong DownloadBlockNumber()
        {
            return _blockNumber;
        }

        public ulong DownloadRoot(string trieName)
        {
            return _trieRootVersions[trieName];
        }

        public IDictionary<ulong, IHashTrieNode> DownloadTrie(string trieName)
        {
            return _trieNodes[trieName];
        }

        private JToken? _CallJsonRPCAPI(string method, JArray param, string _rpcURL)
        {
            JObject options = new JObject
            {
                ["method"] = method,
                ["jsonrpc"] = "2.0",
                ["id"] = "1"
            };
            if (param.Count != 0) options["params"] = param;
            var webRequest = (HttpWebRequest)WebRequest.Create(_rpcURL);
            webRequest.ContentType = "application/json";
            webRequest.Method = "POST";
            using (Stream dataStream = webRequest.GetRequestStream())
            {
                string payloadString = JsonConvert.SerializeObject(options);
                byte[] byteArray = Encoding.UTF8.GetBytes(payloadString);
                dataStream.Write(byteArray, 0, byteArray.Length);
            }

            WebResponse webResponse;
            JObject response;
            using (webResponse = webRequest.GetResponse())
            {
                using (Stream str = webResponse.GetResponseStream()!)
                {
                    using (StreamReader sr = new StreamReader(str))
                    {
                        response = JsonConvert.DeserializeObject<JObject>(sr.ReadToEnd());
                    }
                }
            }
            var result = response["result"];
            return result;
        }
    }
}
