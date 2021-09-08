using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.IO;

namespace Lachain.Core.Network.FastSynchronizerBatch
{
    class Downloader
    {

        private string _blockNumber;
        private PeerManager _peerManager;
        private RequestManager _requestManager;
        private const string EmptyHash = "0x0000000000000000000000000000000000000000000000000000000000000000";
        private const int DefaultTimeout = 10 * 1000; // 10 sec 

        public Downloader(PeerManager peerManager, RequestManager requestManager)
        {
            _peerManager = peerManager;
            _requestManager = requestManager;
            _blockNumber = DownloadLatestBlockNumber();

            System.Console.WriteLine("blocknumber: " + _blockNumber);
        }

        public string GetBlockNumber()
        {
            return _blockNumber;
        }

        public string GetTrie(string trieName)
        {
            string rootHash = DownloadRootHashByTrieName(trieName, _blockNumber);
            System.Console.WriteLine("rootHash: " + rootHash);
            if (!rootHash.Equals(EmptyHash)) _requestManager.AddHash(rootHash);
            while(!_requestManager.Done())
            {
                Console.WriteLine("GetTrie........");
                if(!_peerManager.TryGetPeer(out var peer))
                {
                    Thread.Sleep(100);
                    continue;
                }
                Console.WriteLine("GetTrie after TryGetPeer........");
                if(!_requestManager.TryGetHashBatch(out var hashBatch))
                {
                    _peerManager.TryFreePeer(peer);
                    Thread.Sleep(100);
                    continue;
                }
                Console.WriteLine("GetTrie after TryGetHashBatch........");
                HandleRequest(peer, hashBatch);
            }
            if(!rootHash.Equals(EmptyHash))
            {
                bool flag = _requestManager.CheckConsistency(rootHash);
                System.Console.WriteLine(trieName + " : consistency: " + flag);
            }
            return rootHash;
        }

        private void HandleRequest(Peer peer, List<string> hashBatch)
        {
            System.Console.WriteLine(peer._url);
            JArray hashBatchJson = new JArray { };
            foreach (var hash in hashBatch) hashBatchJson.Add(hash);

            JObject options = new JObject
            {
                ["method"] = "la_getNodeByHashBatch",
                ["jsonrpc"] = "2.0",
                ["id"] = "1",
                ["params"] = new JArray { hashBatchJson }
            };

            try
            {
                HttpWebRequest myHttpWebRequest = (HttpWebRequest)WebRequest.Create(peer._url);
                myHttpWebRequest.ContentType = "application/json";
                myHttpWebRequest.Method = "POST";
                using (Stream dataStream = myHttpWebRequest.GetRequestStream())
                {
                    string payloadString = JsonConvert.SerializeObject(options);
                    byte[] byteArray = Encoding.UTF8.GetBytes(payloadString);
                    dataStream.Write(byteArray, 0, byteArray.Length);
                }

                RequestState myRequestState = new RequestState();
                myRequestState.request = myHttpWebRequest;
                myRequestState.hashBatch = hashBatch;
                myRequestState.peer = peer;
                
                IAsyncResult result =
                    (IAsyncResult)myHttpWebRequest.BeginGetResponse(new AsyncCallback(RespCallback), myRequestState);

                ThreadPool.RegisterWaitForSingleObject(result.AsyncWaitHandle, new WaitOrTimerCallback(TimeoutCallback), myRequestState, DefaultTimeout, true);
            }
            catch (Exception e)
            {
                Console.WriteLine("\nMain Exception raised!");
                Console.WriteLine("Source :{0} ", e.Source);
                Console.WriteLine("Message :{0} ", e.Message);
                _requestManager.HandleResponse(hashBatch, new JArray { });
                _peerManager.TryFreePeer(peer);
            }
        }



        // Abort the request if the timer fires.
        private void TimeoutCallback(object state, bool timedOut)
        {
            if (timedOut)
            {
                RequestState request = state as RequestState;

                if (request != null)
                {
                    request.request.Abort();
                    var peer = request.peer;
                    var hashBatch = request.hashBatch;
                    Console.WriteLine($"timed out from peer {peer._url}");
                    _requestManager.HandleResponse(hashBatch, new JArray { });
                    _peerManager.TryFreePeer(peer);
                }
            }
        }

        private void RespCallback(IAsyncResult asynchronousResult)
        {
            RequestState myRequestState = (RequestState)asynchronousResult.AsyncState;
            var peer = myRequestState.peer;
            var hashBatch = myRequestState.hashBatch;
            JArray nodeBatch = new JArray { };

            try
            {
                // State of request is asynchronous.
                HttpWebRequest myHttpWebRequest = myRequestState.request;
                myRequestState.response = (HttpWebResponse)myHttpWebRequest.EndGetResponse(asynchronousResult);

                WebResponse webResponse;
                JObject response;
                using (webResponse = myRequestState.response)
                {
                    using (Stream str = webResponse.GetResponseStream()!)
                    {
                        using (StreamReader sr = new StreamReader(str))
                        {
                            response = JsonConvert.DeserializeObject<JObject>(sr.ReadToEnd());
                        }
                    }
                }
                nodeBatch = (JArray)response["result"];
            }
            catch (Exception e)
            {
                Console.WriteLine("\nRespCallback Exception raised!");
                Console.WriteLine("\nMessage:{0}", e.Message);
            }
            _requestManager.HandleResponse(hashBatch, nodeBatch);
            _peerManager.TryFreePeer(peer);
        }

        private string DownloadLatestBlockNumber()
        {
            if (_peerManager.GetTotalPeerCount() == 0) throw new Exception("No available peers");
            string blockNumber;

            while (true)
            {
                if (!_peerManager.TryGetPeer(out var peer))
                {
                    throw new Exception("No available peers");
                }

                try
                {
                    blockNumber = (string)SyncRPCApi("eth_blockNumber", new JArray { }, peer._url);
                    if (blockNumber != null && _blockNumber != "0x") // block number is null
                    {
                        _peerManager.TryFreePeer(peer);
                        return blockNumber;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"failed in downloading latest Block Number from peer: {peer._url}");
                }

                _peerManager.TryFreePeer(peer);
            }
        }

        private string DownloadRootHashByTrieName(string trieName, string blockNumber)
        {
            if (_peerManager.GetTotalPeerCount() == 0) throw new Exception("No available peers");
            string rootHash;

            while (true)
            {
                if (!_peerManager.TryGetPeer(out var peer))
                {
                    throw new Exception("No available peers");
                }

                try
                {
                    rootHash = (string)SyncRPCApi("la_getRootHashByTrieName",
                            new JArray { trieName, blockNumber }, peer._url);

                    if (rootHash != null && rootHash != "0x")
                    {
                        _peerManager.TryFreePeer(peer);
                        return rootHash;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"failed in downloading root hash of trie {trieName} from peer: {peer._url}");
                }

                _peerManager.TryFreePeer(peer);
            }
        }

        private JToken SyncRPCApi(string method, JArray param, string _rpcURL)
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
