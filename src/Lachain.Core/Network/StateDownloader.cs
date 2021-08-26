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

        private ulong _blockNumber;
        private string _peerURL;

        public StateDownloader(string peerURL, ulong blockNumber)
        {
            _peerURL = peerURL;
            if(blockNumber == 0)
            {
                blockNumber = Convert.ToUInt64((string)_CallJsonRPCAPI("eth_blockNumber", new JArray { }, peerURL), 16);
            }
            _blockNumber = blockNumber;
            Logger.LogWarning($"initiated State Downloader from {_peerURL} at block number {_blockNumber}");
        }

        public ulong DownloadBlockNumber()
        {
            return _blockNumber;
        }

        public ulong DownloadRoot(string trieName)
        {
            Logger.LogWarning($"downloading root version of {trieName} trie");
            ulong root = Convert.ToUInt64((string)_CallJsonRPCAPI("la_getRootVersionByTrieName",
                new JArray { trieName, Web3DataFormatUtils.Web3Number(_blockNumber) }, _peerURL), 16);
            
            Logger.LogWarning($"Completed downloading root version of {trieName} trie, root = {root}");
            return root;
        }

        public IDictionary<ulong, IHashTrieNode> DownloadTrie(string trieName)
        {
            Logger.LogWarning($"downloading {trieName} trie");
            IDictionary<ulong, IHashTrieNode> trie = new Dictionary<ulong, IHashTrieNode>();
            Queue<ulong> queue = new Queue<ulong>();
            ulong root = DownloadRoot(trieName);
            if(root > 0)
            {
                trie[root] = DownloadNode(root);

                if (trie[root].Type == NodeType.Internal) queue.Enqueue(root);

                while (queue.Count > 0)
                {
                    ulong cur = queue.Dequeue();
                    IDictionary<ulong, IHashTrieNode> childs = DownloadChild(cur);
                    foreach (var item in childs)
                    {
                        ulong version = item.Key;
                        IHashTrieNode child = item.Value;
                        trie[version] = child;
                        if (child.Type == NodeType.Internal)
                        {
                            queue.Enqueue(version);
                        }
                    }
                }
            }
            Logger.LogWarning($"{trieName} download done");
            return trie;
        }

        private IHashTrieNode DownloadNode(ulong version)
        {
            Logger.LogWarning($"downloding node with version {version}");
            JObject? receivedInfo = (JObject?)_CallJsonRPCAPI("la_getNodeByVersion", new JArray { Web3DataFormatUtils.Web3Number(version) }, _peerURL);
            IHashTrieNode node = Web3DataFormatUtils.NodeFromJson(receivedInfo);
            Logger.LogWarning($"Completed downloding node with version {version}");
            return node; 
        }

        IDictionary<ulong, IHashTrieNode> DownloadChild(ulong version)
        {
            Logger.LogWarning($"downloding childs with of node with version {version}");
            JObject? receivedInfo = (JObject?)_CallJsonRPCAPI("la_getChildByVersion", new JArray { Web3DataFormatUtils.Web3Number(version) }, _peerURL);
            IDictionary<ulong, IHashTrieNode> childs = Web3DataFormatUtils.TrieFromJson(receivedInfo);
            Logger.LogWarning($"Completed downloding childs with of node with version {version}");
            return childs;
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
