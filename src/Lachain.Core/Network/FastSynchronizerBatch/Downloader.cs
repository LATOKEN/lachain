/*
    Most crucial for downloading the 6 state tries. It is written in a way that we can download different batch of nodes
    concurrently(with asynchronous programming).
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Lachain.Core.RPC.HTTP.Web3;
using Lachain.Logger;
using Lachain.Networking;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Utility.Utils;
using Google.Protobuf;


namespace Lachain.Core.Network.FastSynchronizerBatch
{
    class Downloader
    {

        private ulong _blockNumber;
        private readonly INetworkManager _networkManager;
        private PeerManager _peerManager;
        private RequestManager _requestManager;
        private readonly UInt256 EmptyHash = UInt256Utils.Zero;
        private const int DefaultTimeout = 5 * 1000; // 5 sec 
        private BlockRequestManager _blockRequestManager; 
        private static readonly ILogger<Downloader> Logger = LoggerFactory.GetLoggerForClass<Downloader>();

        public Downloader(INetworkManager networkManager, PeerManager peerManager, RequestManager requestManager)
        {
            _networkManager = networkManager;
            _peerManager = peerManager;
            _requestManager = requestManager;
            _blockNumber = DownloadLatestBlockNumber();

            System.Console.WriteLine("blocknumber: " + _blockNumber);
        }

        public Downloader(INetworkManager networkManager, PeerManager peerManager, RequestManager requestManager, ulong blockNumber)
        {
            _networkManager = networkManager;
            _peerManager = peerManager;
            _requestManager = requestManager;
            if(blockNumber == 0) _blockNumber = DownloadLatestBlockNumber();
            else _blockNumber = blockNumber;

            System.Console.WriteLine("blocknumber: " + _blockNumber);
        }

        public ulong GetBlockNumber()
        {
            return _blockNumber;
        }

        public UInt256 GetTrie(string trieName, NodeStorage _nodeStorage)
        {
            var rootHash = DownloadRootHashByTrieName(trieName, _blockNumber);
            Logger.LogInformation("Inside Get Trie. rootHash: " + rootHash.ToHex());
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
                HandleNodeRequest(peer, hashBatch);
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

        public void HandleNodeRequest(Peer peer, List<UInt256> batch)
        {
            var message = _networkManager.MessageFactory.TrieNodeByHashRequest(batch);
            _networkManager.SendTo(peer._publicKey, message);
        }

        public void HandleBlockRequest(Peer peer, List<ulong> batch)
        {
            var message = _networkManager.MessageFactory.BlockBatchRequest(batch);
            _networkManager.SendTo(peer._publicKey, message);
        }

//         private void HandleRequest(Peer peer, List<string> batch, uint type)
//         {
//             DateTime t1 = DateTime.Now; 
//             JArray batchJson = new JArray { };
//             foreach (var item in batch) batchJson.Add(item);

//             JObject options = new JObject
//             {
//                 ["jsonrpc"] = "2.0",
//                 ["id"] = "1",
//                 ["params"] = new JArray { batchJson }
//             };
//             if(type==1) options["method"] = "la_getNodeByHashBatch";
//             else options["method"] = "la_getBlockRawByNumberBatch";
//             try
//             {
//                 HttpWebRequest myHttpWebRequest = (HttpWebRequest)WebRequest.Create(peer._url);
//                 myHttpWebRequest.ContentType = "application/json";
//                 myHttpWebRequest.Method = "POST";
//                 using (Stream dataStream = myHttpWebRequest.GetRequestStream())
//                 {
//                     string payloadString = JsonConvert.SerializeObject(options);
//                     byte[] byteArray = Encoding.UTF8.GetBytes(payloadString);
//                     dataStream.Write(byteArray, 0, byteArray.Length);
//                 }

//                 RequestState myRequestState = new RequestState();
//                 myRequestState.request = myHttpWebRequest;
//                 myRequestState.batch = batch;
//                 myRequestState.peer = peer;
//                 myRequestState.type = type;
//                 myRequestState.start = DateTime.Now;

//                 DateTime t2 = DateTime.Now;

// //                Logger.LogInformation($"Object ready for sending to peer{peer._url}, spent time:{(t2-t1).TotalMilliseconds}");

//                 IAsyncResult result =
//                     (IAsyncResult)myHttpWebRequest.BeginGetResponse(new AsyncCallback(RespCallback), myRequestState);
                

//                 ThreadPool.RegisterWaitForSingleObject(result.AsyncWaitHandle, new WaitOrTimerCallback(TimeoutCallback), myRequestState, DefaultTimeout, true);
//             }
//             catch (Exception e)
//             {
//                 Logger.LogWarning("\nMain Exception raised!");
//                 Logger.LogWarning("Source :{0} ", e.Source);
//                 Logger.LogWarning("Message :{0} ", e.Message);
//                 if(_peerManager.TryFreePeer(peer, 0))
//                 {
//                     if(type==1) _requestManager.HandleResponse(batch, new JArray { });
//                     if(type==2) _blockRequestManager.HandleResponse(batch, new JArray{ });
//                 }
//             }
//         }



//         // Abort the request if the timer fires.
//         private void TimeoutCallback(object state, bool timedOut)
//         {
//             if (timedOut)
//             {
//                 RequestState request = state as RequestState;
//                 TimeSpan time = DateTime.Now - request.start; 
//                 if (request != null)
//                 {
//                     request.request.Abort();
//                     var peer = request.peer;
//                     var batch = request.batch;
//                     Logger.LogWarning($"timed out from peer {peer._url} spent {time.TotalMilliseconds}   : {batch[0]}");
//                     _peerManager.TryFreePeer(peer, 0);
//                     if(request.type==1) _requestManager.HandleResponse(batch, new JArray { });
//                     if(request.type==2) _blockRequestManager.HandleResponse(batch, new JArray{ });
//                 }
//             }
//         }

        // private void RespCallback(IAsyncResult asynchronousResult)
        // {
        //     RequestState myRequestState = (RequestState)asynchronousResult.AsyncState;
        //     TimeSpan time = DateTime.Now - myRequestState.start;
        //     DateTime receiveTime = DateTime.Now;
        //     var peer = myRequestState.peer;
        //     var batch = myRequestState.batch;
        //     JArray result = new JArray { };

        //     try
        //     {
        //         // State of request is asynchronous.
        //         HttpWebRequest myHttpWebRequest = myRequestState.request;
        //         myRequestState.response = (HttpWebResponse)myHttpWebRequest.EndGetResponse(asynchronousResult);

        //         WebResponse webResponse;
        //         JObject response;
        //         using (webResponse = myRequestState.response)
        //         {
        //             using (Stream str = webResponse.GetResponseStream()!)
        //             {
        //                 using (StreamReader sr = new StreamReader(str))
        //                 {
        //                     response = JsonConvert.DeserializeObject<JObject>(sr.ReadToEnd());
        //                 }
        //             }
        //         }
        //         result = (JArray)response["result"];
        //         Logger.LogInformation($"Received data {myRequestState.type} size:{batch.Count}  time spent:{time.TotalMilliseconds} from peer:{peer._url}, preparation time:{(DateTime.Now-receiveTime).TotalMilliseconds}");
        //         _peerManager.TryFreePeer(peer, 1);
        //         if(myRequestState.type==1) _requestManager.HandleResponse(batch, result);
        //         if(myRequestState.type==2) _blockRequestManager.HandleResponse(batch, result);
        //     }
        //     catch (Exception e)
        //     {
        //         Logger.LogWarning("\nRespCallback Exception raised!");
        //         Logger.LogWarning("\nMessage:{0}", e.Message);
        //         Logger.LogWarning($"Wasted time:{time.TotalMilliseconds} from peer:{peer._url}  :  {batch[0]}");
        //         _peerManager.TryFreePeer(peer, 0);
        //         if(myRequestState.type==1) _requestManager.HandleResponse(batch, result);
        //         if(myRequestState.type==2) _blockRequestManager.HandleResponse(batch, result);
        //     }
        // }

        private ulong DownloadLatestBlockNumber()
        {
            if (_peerManager.GetTotalPeerCount() == 0) throw new Exception("No available peers");
            ulong? blockNumber;

            while (true)
            {
                if (!_peerManager.TryGetPeer(out var peer))
                {
                    throw new Exception("No available peers");
                }
                blockNumber = DownloadLatestBlockNumber(peer);

                _peerManager.TryFreePeer(peer);

                if(blockNumber != null) return blockNumber.Value;  
            }
        }

        private ulong? DownloadLatestBlockNumber(Peer peer)
        {
            ulong? blockNumber = null;
            try
            {
                blockNumber = DownloadLatestBlockNumberFromPeer(peer);
            }
            catch (Exception e)
            {
                Logger.LogWarning($"failed in downloading latest Block Number from peer: {peer._publicKey}");
                Logger.LogWarning("\nMessage:{0}", e.Message);
            }
            return blockNumber;
        }

        private ulong? DownloadLatestBlockNumberFromPeer(Peer peer)
        {
            return _peerManager.GetHeightForPeer(peer);
        }

        public UInt256 DownloadRootHashByTrieName(string trieName)
        {
            return DownloadRootHashByTrieName(trieName, _blockNumber);
        }

        private UInt256 DownloadRootHashByTrieName(string trieName, ulong blockNumber)
        {
            return DownloadRootHashByTrieNameFromApi(trieName, Web3DataFormatUtils.Web3Number(blockNumber)).HexToUInt256();
        }

        private string DownloadRootHashByTrieNameFromApi(string trieName, string blockNumber)
        {
            if (_peerManager.GetTotalPeerCount() == 0) throw new Exception("No available peers");
            string rootHash;

            while (true)
            {
                if (!_peerManager.TryGetPeer(out var peer))
                {
                    throw new Exception("No available peers");
                }
                Logger.LogWarning("Trying to download root hash from peer: " + peer!._publicKey);
                try
                {
                    rootHash = (string)SyncRPCApi("la_getRootHashByTrieName",
                            new JArray { trieName, blockNumber }, peer._url);
                    //rootHash = HandleRootHashRequest(peer, trieName, blockNumber);

                    if (rootHash != null && rootHash != "0x")
                    {
                        _peerManager.TryFreePeer(peer);
                        return rootHash;
                    }
                }
                catch (Exception e)
                {
                    Logger.LogWarning($"failed in downloading root hash of trie {trieName} from peer: {peer._url}");
                }

                _peerManager.TryFreePeer(peer);
            }
        }

        public void DownloadBlocks(NodeStorage nodeStorage, IBlockSnapshot blockSnapshot)
        {
            _blockRequestManager = new BlockRequestManager(blockSnapshot, _blockNumber, nodeStorage);

            while (!_blockRequestManager.Done())
            {
                if(!_peerManager.TryGetPeer(out var peer))
                {
                    Thread.Sleep(200);
                    continue;
                }
                if(!_blockRequestManager.TryGetBatch(out var batch))
                {
                    _peerManager.TryFreePeer(peer);
                    Thread.Sleep(500);
                    continue;
                }
                HandleBlockRequest(peer, batch);
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
            webRequest.Timeout = 10*1000;
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
