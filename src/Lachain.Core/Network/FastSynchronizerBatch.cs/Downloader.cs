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
using Lachain.Core.RPC.HTTP.Web3;
using Lachain.Storage.State;
using Lachain.Utility.Utils;
using Google.Protobuf;

namespace Lachain.Core.Network.FastSynchronizerBatch
{
    class Downloader
    {

        private string _blockNumber;
        private PeerManager _peerManager;
        private RequestManager _requestManager;
        private const string EmptyHash = "0x0000000000000000000000000000000000000000000000000000000000000000";
        private const int DefaultTimeout = 5 * 1000; // 5 sec 
        private BlockRequestManager _blockRequestManager; 
        public Downloader(PeerManager peerManager, RequestManager requestManager)
        {
            _peerManager = peerManager;
            _requestManager = requestManager;
            _blockNumber = DownloadLatestBlockNumber();

            System.Console.WriteLine("blocknumber: " + Convert.ToUInt64(_blockNumber,16));
        }

        public Downloader(PeerManager peerManager, RequestManager requestManager, ulong blockNumber)
        {
            _peerManager = peerManager;
            _requestManager = requestManager;
            if(blockNumber == 0) _blockNumber = DownloadLatestBlockNumber();
            else _blockNumber = Web3DataFormatUtils.Web3Number(blockNumber);

            System.Console.WriteLine("blocknumber: " + Convert.ToUInt64(_blockNumber,16));
        }

        public string GetBlockNumber()
        {
            return _blockNumber;
        }

        public string GetTrie(string trieName, NodeStorage _nodeStorage)
        {
            string rootHash = DownloadRootHashByTrieName(trieName, _blockNumber);
            System.Console.WriteLine("Inside Get Trie. rootHash: " + rootHash);
            if (!rootHash.Equals(EmptyHash))
            {
                bool foundHash = _nodeStorage.GetIdByHash(rootHash, out var id);
                if(!foundHash) _requestManager.AddHash(rootHash);
            }
            while(!_requestManager.Done())
            {
            //    Console.WriteLine("GetTrie........");
                if(!_peerManager.TryGetPeer(out var peer))
                {
                    Thread.Sleep(500);
                    continue;
                }
            //    Console.WriteLine("GetTrie after TryGetPeer........");
                if(!_requestManager.TryGetHashBatch(out var hashBatch))
                {
                    _peerManager.TryFreePeer(peer);
                    Thread.Sleep(500);
                    continue;
                }
            //    Console.WriteLine("GetTrie after TryGetHashBatch........");
                HandleRequest(peer, hashBatch, 1);
            }
            _nodeStorage.Commit();
            if(!rootHash.Equals(EmptyHash))
            {
    //            bool res =_nodeStorage.GetIdByHash(rootHash,out ulong id);
    //            bool flag = _requestManager.CheckConsistency(id);
    //            System.Console.WriteLine(trieName + " : consistency: " + flag);
            }
            return rootHash;
        }

        private void HandleRequest(Peer peer, List<string> batch, uint type)
        {
        //    System.Console.WriteLine(peer._url);
            JArray batchJson = new JArray { };
            foreach (var item in batch) batchJson.Add(item);

            JObject options = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = "1",
                ["params"] = new JArray { batchJson }
            };
            if(type==1) options["method"] = "la_getNodeByHashBatch";
            else options["method"] = "la_getBlockRawByNumberBatch";
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
                myRequestState.batch = batch;
                myRequestState.peer = peer;
                myRequestState.type = type;
                myRequestState.start = DateTime.Now;

                IAsyncResult result =
                    (IAsyncResult)myHttpWebRequest.BeginGetResponse(new AsyncCallback(RespCallback), myRequestState);

                ThreadPool.RegisterWaitForSingleObject(result.AsyncWaitHandle, new WaitOrTimerCallback(TimeoutCallback), myRequestState, DefaultTimeout, true);
            }
            catch (Exception e)
            {
                Console.WriteLine("\nMain Exception raised!");
                Console.WriteLine("Source :{0} ", e.Source);
                Console.WriteLine("Message :{0} ", e.Message);
                if(_peerManager.TryFreePeer(peer, 0))
                {
                    if(type==1) _requestManager.HandleResponse(batch, new JArray { });
                    if(type==2) _blockRequestManager.HandleResponse(batch, new JArray{ });
                }
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
                    var batch = request.batch;
                    TimeSpan time = DateTime.Now - request.start; 
                    Console.WriteLine($"timed out from peer {peer._url} spent {time.TotalMilliseconds}   : {batch[0]}");
                    if(_peerManager.TryFreePeer(peer, 0))
                    {
                        if(request.type==1) _requestManager.HandleResponse(batch, new JArray { });
                        if(request.type==2) _blockRequestManager.HandleResponse(batch, new JArray{ });
                    }
                }
            }
        }

        private void RespCallback(IAsyncResult asynchronousResult)
        {
            RequestState myRequestState = (RequestState)asynchronousResult.AsyncState;
            var peer = myRequestState.peer;
            var batch = myRequestState.batch;
            JArray result = new JArray { };

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
                result = (JArray)response["result"];
                TimeSpan time = DateTime.Now - myRequestState.start;
                Console.WriteLine($"Received data {myRequestState.type} size:{batch.Count}  time spent:{time.TotalMilliseconds} from peer:{peer._url}");
            }
            catch (Exception e)
            {
                Console.WriteLine("\nRespCallback Exception raised!");
                Console.WriteLine("\nMessage:{0}", e.Message);
                TimeSpan time = DateTime.Now - myRequestState.start;
                Console.WriteLine($"Wasted time:{time.TotalMilliseconds} from peer:{peer._url}  :  {batch[0]}");
            }
            if(_peerManager.TryFreePeer(peer, 1))
            {
                if(myRequestState.type==1) _requestManager.HandleResponse(batch, result);
                if(myRequestState.type==2) _blockRequestManager.HandleResponse(batch, result);
            }
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
                blockNumber = DownloadLatestBlockNumber(peer);

                _peerManager.TryFreePeer(peer);

                if(blockNumber != "0x") return blockNumber;  
            }
        }

        private string DownloadLatestBlockNumber(Peer peer)
        {
            string blockNumber = "0x";
            try
            {
                blockNumber = (string)SyncRPCApi("eth_blockNumber", new JArray { }, peer._url);
            }
            catch (Exception e)
            {
                Console.WriteLine($"failed in downloading latest Block Number from peer: {peer._url}");
                Console.WriteLine("\nMessage:{0}", e.Message);
            }
            if(blockNumber is null) blockNumber = "0x";
            return blockNumber;
        }

        public string DownloadRootHashByTrieName(string trieName)
        {
            return DownloadRootHashByTrieName(trieName, _blockNumber);
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

        public void DownloadBlocks(NodeStorage nodeStorage, IBlockSnapshot blockSnapshot)
        {
            _blockRequestManager = new BlockRequestManager(blockSnapshot, Convert.ToUInt64(_blockNumber,16), nodeStorage);

            while (!_blockRequestManager.Done())
            {
                if(!_peerManager.TryGetPeer(out var peer))
                {
                    Thread.Sleep(100);
                    continue;
                }
                if(!_blockRequestManager.TryGetBatch(out var batch))
                {
                    _peerManager.TryFreePeer(peer);
                    Thread.Sleep(100);
                    continue;
                }
                HandleRequest(peer, batch, 2);
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
