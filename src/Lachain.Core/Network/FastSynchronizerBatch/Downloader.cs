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
    public class Downloader : IDownloader
    {

        private ulong _totalRequests;
        private const uint _requestUpdatePeriod = 10000;
        private readonly INetworkManager _networkManager;
        private readonly PeerManager _peerManager;
        private readonly IRequestManager _requestManager;
        private readonly IFastSyncRepository _repository;
        private readonly UInt256 EmptyHash = UInt256Utils.Zero;
        private const int DefaultTimeout = 5 * 1000; // 5 sec 
        private readonly IBlockRequestManager _blockRequestManager; 
        private IDictionary<ulong, RequestState> _requests = new Dictionary<ulong, RequestState>();
        private static readonly ILogger<Downloader> Logger = LoggerFactory.GetLoggerForClass<Downloader>();

        public Downloader(
            INetworkManager networkManager,
            IRequestManager requestManager,
            IBlockRequestManager blockRequestManager,
            IFastSyncRepository repository
        )
        {
            _networkManager = networkManager;
            _requestManager = requestManager;
            _blockRequestManager = blockRequestManager;
            _repository = repository;
            _peerManager = new PeerManager();
        }

        public PeerManager GetPeerManager()
        {
            return _peerManager;
        }

        public void GetTrie(UInt256 rootHash)
        {
            Logger.LogTrace($"Inside Get Trie. rootHash: {rootHash.ToHex()}");
            if (!rootHash.Equals(EmptyHash))
            {
                bool foundHash = _repository.GetIdByHash(rootHash, out var id);
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
                    _peerManager.TryFreePeer(peer!);
                    Thread.Sleep(500);
                    continue;
                }
            //    Console.WriteLine("GetTrie after TryGetHashBatch........");
                Logger.LogInformation($"Preparing nodes request to send to peer {peer!._publicKey.ToHex()}");
                _totalRequests++;
                UpdateTotalRequest();
                var myRequestState = new RequestState(RequestType.NodesRequest, hashBatch, DateTime.Now, peer!, _totalRequests);
                _requests[_totalRequests] = myRequestState;
                HandleRequest(myRequestState);
            }
            _repository.Commit();
            if(!rootHash.Equals(EmptyHash))
            {
    //            bool res =_repository.GetIdByHash(rootHash,out ulong id);
    //            bool flag = _requestManager.CheckConsistency(id);
    //            System.Console.WriteLine(trieName + " : consistency: " + flag);
            }
        }

        // Handling request asynchronously
        private void HandleRequest(RequestState request)
        {
            try
            {
                NetworkMessage message;
                if (request._type == RequestType.BlocksRequest)
                {
                    message = _networkManager.MessageFactory.BlockBatchRequest(request._blockBatch!, request._requestId);
                }
                else
                {
                    message = _networkManager.MessageFactory.TrieNodeByHashRequest(request._nodeBatch!, request._requestId);
                } ;
                Logger.LogInformation($"Object ready for sending to peer{request._peer._publicKey.ToHex()}, "
                    + $"spent time:{(DateTime.Now - request._start).TotalMilliseconds}");
                _networkManager.SendTo(request._peer._publicKey, message);
                TimeOut(request._peerHasReply, request._requestId);
            }
            catch (Exception exception)
            {
                Logger.LogWarning($"Exception raised while trying to send request {request._type} "
                    + $"to peer {request._peer._publicKey.ToHex()} : {exception}");
                if(_peerManager.TryFreePeer(request._peer, false))
                {
                    if (request._type == RequestType.BlocksRequest)
                    {
                        _blockRequestManager.HandleResponse(request._blockBatch!, new List<Block>());
                    }
                    else
                    {
                        _requestManager.HandleResponse(request._nodeBatch!, new List<TrieNodeInfo>());
                    }
                }
            }
        }

        private async void TimeOut(object peerHasReply, ulong requestId)
        {
            await Task.Run(() =>
            {
                lock(peerHasReply)
                {
                    bool gotReply = Monitor.Wait(peerHasReply, TimeSpan.FromMilliseconds(DefaultTimeout));
                    // Abort the request if the timer fires.
                    if (!gotReply && _requests.TryGetValue(requestId, out var request))
                    {
                        _requests.Remove(requestId);
                        if (request != null)
                        {
                            var peer = request._peer;
                            TimeSpan time = DateTime.Now - request._start; 
                            Logger.LogWarning($"timed out from peer {peer._publicKey.ToHex()} spent {time.TotalMilliseconds}");
                            _peerManager.TryFreePeer(peer, false);
                            switch (request._type)
                            {
                                case RequestType.NodesRequest:
                                    _requestManager.HandleResponse(request._nodeBatch!, new List<TrieNodeInfo>());
                                    break;

                                case RequestType.BlocksRequest:
                                    _blockRequestManager.HandleResponse(request._blockBatch!, new List<Block>());
                                    break;

                                default:
                                    Logger.LogWarning($"Unsupported request: {request._type}");
                                    break;
                            }
                        }
                    }
                }
            });
        }

        public void HandleBlocksFromPeer(List<Block> response, ulong requestId, ECDSAPublicKey publicKey)
        {
            if (_requests.TryGetValue(requestId, out var request))
            {
                _requests.Remove(requestId);
                TimeSpan time = DateTime.Now - request._start;
                DateTime receiveTime = DateTime.Now;
                var peer = request._peer;
                var batch = request._blockBatch;
                // Let the TimeOut know that we got the response
                lock (request._peerHasReply)
                {
                    Monitor.PulseAll(request._peerHasReply);
                }
                try
                {
                    if (peer._publicKey != publicKey || request._type != RequestType.BlocksRequest)
                    {
                        Logger.LogWarning($"Asked for blocks to peer: {peer._publicKey.ToHex()} with  request id: "
                            + $"{request._requestId} and request type: {request._type}, got reply from peer: {publicKey.ToHex()}");
                        throw new Exception($"Invalid reply from peer: {publicKey.ToHex()}");
                    }
                    Logger.LogInformation($"Received data {request._type} size:{batch!.Count}  time spent:{time.TotalMilliseconds}"
                        + $" from peer:{peer._publicKey.ToHex()}, preparation time:{(DateTime.Now-receiveTime).TotalMilliseconds}");
                    _peerManager.TryFreePeer(peer, true);
                    _blockRequestManager.HandleResponse(batch!, response);
                }
                catch (Exception exception)
                {
                    Logger.LogWarning($"Exception raised while handling blocks from peer: {publicKey.ToHex()} : {exception}");
                    Logger.LogWarning($"Wasted time:{time.TotalMilliseconds} from peer:{peer._publicKey.ToHex()}");
                    _peerManager.TryFreePeer(peer, false);
                    _blockRequestManager.HandleResponse(batch!, new List<Block>());
                }
            }
        }

        public void HandleNodesFromPeer(List<TrieNodeInfo> response, ulong requestId, ECDSAPublicKey publicKey)
        {
            if (_requests.TryGetValue(requestId, out var request))
            {
                _requests.Remove(requestId);
                TimeSpan time = DateTime.Now - request._start;
                DateTime receiveTime = DateTime.Now;
                var peer = request._peer;
                var batch = request._nodeBatch;
                // Let the TimeOut know that we got the response
                lock (request._peerHasReply)
                {
                    Monitor.PulseAll(request._peerHasReply);
                }
                try
                {
                    if (peer._publicKey != publicKey || request._type != RequestType.NodesRequest) 
                    {
                        Logger.LogWarning($"Asked for nodes to peer: {peer._publicKey.ToHex()} with  request id: "
                            + $"{request._requestId} and request type: {request._type}, got reply from peer: {publicKey.ToHex()}");
                        throw new Exception($"Invalid reply from peer: {publicKey.ToHex()}");
                    }
                    Logger.LogInformation($"Received data {request._type} size:{batch!.Count}  time spent:{time.TotalMilliseconds}"
                        + $" from peer:{peer._publicKey.ToHex()}, preparation time:{(DateTime.Now-receiveTime).TotalMilliseconds}");
                    _peerManager.TryFreePeer(peer, true);
                    _requestManager.HandleResponse(batch!, response);
                }
                catch (Exception exception)
                {
                    Logger.LogWarning($"Exception raised while handling nodes from peer: {publicKey.ToHex()} : {exception}");
                    Logger.LogWarning($"Wasted time:{time.TotalMilliseconds} from peer:{peer._publicKey.ToHex()}");
                    _peerManager.TryFreePeer(peer, false);
                    _requestManager.HandleResponse(batch!, new List<TrieNodeInfo>());
                }
            }
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

        public void DownloadBlocks()
        {

            while (!_blockRequestManager.Done())
            {
                if(!_peerManager.TryGetPeer(out var peer))
                {
                    Thread.Sleep(200);
                    continue;
                }
                if(!_blockRequestManager.TryGetBatch(out var batch))
                {
                    _peerManager.TryFreePeer(peer!);
                    Thread.Sleep(500);
                    continue;
                }
                Logger.LogInformation($"Preparing blocks request to send to peer {peer!._publicKey.ToHex()}");
                _totalRequests++;
                UpdateTotalRequest();
                var myRequestState = new RequestState(RequestType.BlocksRequest, batch, DateTime.Now, peer!, _totalRequests);
                _requests[_totalRequests] = myRequestState;
                HandleRequest(myRequestState);
            }
        }

        private void UpdateTotalRequest()
        {
            if (_totalRequests % _requestUpdatePeriod == 0)
                _repository.SetTotalRequests(_totalRequests);
        }

    }
}
