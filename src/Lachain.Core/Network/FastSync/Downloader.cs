/*
    Most crucial for downloading the 6 state tries. It is written in a way that we can download different batch of nodes
    concurrently(with asynchronous programming).
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lachain.Core.Blockchain.Checkpoints;
using Lachain.Core.Blockchain.Error;
using Lachain.Logger;
using Lachain.Networking;
using Lachain.Proto;
using Lachain.Utility.Utils;
using Google.Protobuf;
using Lachain.Logger;


namespace Lachain.Core.Network.FastSync
{
    class Downloader
    {

        private string _blockNumber;
        private PeerManager _peerManager;
        private RequestManager _requestManager;
        private const string EmptyHash = "0x0000000000000000000000000000000000000000000000000000000000000000";
        private const int DefaultTimeout = 5 * 1000; // 5 sec 
        private BlockRequestManager _blockRequestManager; 
        private static readonly ILogger<Downloader> Logger = LoggerFactory.GetLoggerForClass<Downloader>();
        private Block? _checkpointBlock;
        private List<(UInt256, CheckpointType)>? _checkpointStateHashes;
        public Block? CheckpointBlock => _checkpointBlock;
        public List<(UInt256, CheckpointType)>? CheckpointStateHashes => _checkpointStateHashes;

        public Downloader(
            INetworkManager networkManager,
            IRequestManager requestManager,
            IBlockRequestManager blockRequestManager,
            IFastSyncRepository repository
        )
        {
            _peerManager = peerManager;
            _requestManager = requestManager;
            _blockNumber = DownloadLatestBlockNumber();

            System.Console.WriteLine("blocknumber: " + Convert.ToUInt64(_blockNumber,16));
        }

        public string GetBlockNumber()
        {
            return _blockNumber;
        }

        public string GetTrie(string trieName, NodeStorage _nodeStorage)
        {
            string rootHash = DownloadRootHashByTrieName(trieName, _blockNumber);
            Logger.LogInformation("Inside Get Trie. rootHash: " + rootHash);
            if (!rootHash.Equals(EmptyHash))
            {
                bool foundHash = _nodeStorage.GetIdByHash(rootHash, out var id);
                if(!foundHash) _requestManager.AddHash(rootHash);
            }

            var requestTime = TimeUtils.CurrentTimeMillis();
            ulong alertTime = 60 * 1000; // 1 min
            while(_requests.Count != 0 || !_requestManager.Done())
            {
                var timePassed = TimeUtils.CurrentTimeMillis() - requestTime;
                if (timePassed >= alertTime)
                {
                    Logger.LogWarning($"Waiting to get hash batch for too long, time passed: {timePassed / 1000.0} seconds");
                }
                var peer = GetPeer();
                if(!_requestManager.TryGetHashBatch(out var hashBatch, out var batchId))
                {
                    _peerManager.TryFreePeer(peer);
                    Thread.Sleep(500);
                    continue;
                }
                Logger.LogInformation($"Preparing nodes request to send to peer {peer!._publicKey.ToHex()}");
                var myRequestState = new RequestState(RequestType.NodesRequest, hashBatch, batchId, peer!);
                HandleRequest(myRequestState);
                requestTime = TimeUtils.CurrentTimeMillis();
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
                Logger.LogWarning("\nMain Exception raised!");
                Logger.LogWarning("Source :{0} ", e.Source);
                Logger.LogWarning("Message :{0} ", e.Message);
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
                TimeSpan time = DateTime.Now - request.start; 
                if (request != null)
                {
                    request.request.Abort();
                    var peer = request.peer;
                    var batch = request.batch;
                    Logger.LogWarning($"timed out from peer {peer._url} spent {time.TotalMilliseconds}   : {batch[0]}");
                    _peerManager.TryFreePeer(peer, 0);
                    if(request.type==1) _requestManager.HandleResponse(batch, new JArray { });
                    if(request.type==2) _blockRequestManager.HandleResponse(batch, new JArray{ });
                }
            }
        }

        private void RespCallback(IAsyncResult asynchronousResult)
        {
            RequestState myRequestState = (RequestState)asynchronousResult.AsyncState;
            TimeSpan time = DateTime.Now - myRequestState.start;
            var peer = myRequestState.peer;
            var batch = myRequestState.batch;
            JArray result = new JArray { };

            try
            {
                Logger.LogTrace("HandleBlocksFromPeer");
                lock (request._peerHasReply)
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
                Logger.LogInformation($"Received data {myRequestState.type} size:{batch.Count}  time spent:{time.TotalMilliseconds} from peer:{peer._url}");
                _peerManager.TryFreePeer(peer, 1);
                if(myRequestState.type==1) _requestManager.HandleResponse(batch, result);
                if(myRequestState.type==2) _blockRequestManager.HandleResponse(batch, result);
            }
            catch (Exception e)
            {
                Logger.LogWarning("\nRespCallback Exception raised!");
                Logger.LogWarning("\nMessage:{0}", e.Message);
                Logger.LogWarning($"Wasted time:{time.TotalMilliseconds} from peer:{peer._url}  :  {batch[0]}");
                _peerManager.TryFreePeer(peer, 0);
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
                Logger.LogTrace("HandleNodesFromPeer");
                lock (request._peerHasReply)
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
                Logger.LogTrace("HandleCheckpointBlockFromPeer");
                lock (request._peerHasReply)
                {
                    _requests.Remove(requestId);
                    TimeSpan time = DateTime.Now - request._start;
                    DateTime receiveTime = DateTime.Now;
                    var peer = request._peer;
                    var blockNumber = request._blockNumber;
                    // Let the TimeOut know that we got the response
                    Monitor.PulseAll(request._peerHasReply);
                
                    try
                    {
                        if (block is null || !peer._publicKey.Equals(publicKey) || request._type != RequestType.CheckpointBlockRequest) 
                        {
                            Logger.LogWarning($"Asked for checkpoint block {blockNumber} to peer: {peer._publicKey.ToHex()} with request id: "
                                + $"{request._requestId} and request type: {request._type}, got reply from peer: {publicKey.ToHex()}");
                            if (block is null) Logger.LogWarning("Found null block");
                            if (!peer._publicKey.Equals(publicKey))
                            {
                                Logger.LogWarning($"Sent to {peer._publicKey.ToHex()}, but got reply from {publicKey.ToHex()}");
                            }
                            if (request._type != RequestType.CheckpointBlockRequest)
                            {
                                Logger.LogWarning($"Got request type: {request._type} instead of {RequestType.CheckpointBlockRequest}");
                            }
                            throw new Exception($"Invalid reply from peer: {publicKey.ToHex()}");
                        }
                        Logger.LogInformation($"Received data {request._type} time spent:{time.TotalMilliseconds}"
                            + $" from peer:{peer._publicKey.ToHex()}, preparation time:{(DateTime.Now-receiveTime).TotalMilliseconds}");
                        
                        // Setting checkValidatorSet = false because we don't have validator set.
                        var result = _blockRequestManager.VerifyBlock(block);
                        if (result != OperatingError.Ok)
                        {
                            Logger.LogDebug($"Block Verification failed with: {result}");
                            throw new Exception("Block verification failed");
                        }
                        _peerManager.TryFreePeer(peer, true);
                        Logger.LogInformation("Fetched checkpoint block successfully");
                        _checkpointBlock = block;
                    }
                    catch (Exception exception)
                    {
                        Logger.LogWarning($"Exception raised while handling nodes from peer: {publicKey.ToHex()} : {exception}");
                        Logger.LogWarning($"Wasted time:{time.TotalMilliseconds} from peer:{peer._publicKey.ToHex()}");
                        _peerManager.TryFreePeer(peer, false);
                        // Try again
                        Task.Factory.StartNew(() =>
                        {
                            DownloadCheckpointBlock(request._blockNumber!.Value);
                        }, TaskCreationOptions.LongRunning);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogWarning($"failed in downloading latest Block Number from peer: {peer._url}");
                Logger.LogWarning("\nMessage:{0}", e.Message);
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
                Logger.LogTrace("HandleCheckpointStateHashFromPeer");
                lock (request._peerHasReply)
                {
                    throw new Exception("No available peers");
                }
                Logger.LogWarning("Trying to download root hash from peer: "+peer._url);
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
                    Logger.LogWarning($"failed in downloading root hash of trie {trieName} from peer: {peer._url}");
                }

                _peerManager.TryFreePeer(peer);
            }
        }

        public void DownloadBlocks(NodeStorage nodeStorage, IBlockSnapshot blockSnapshot)
        {
            _blockRequestManager = new BlockRequestManager(blockSnapshot, Convert.ToUInt64(_blockNumber,16), nodeStorage);

            var requestTime = TimeUtils.CurrentTimeMillis();
            ulong alertTime = 60 * 1000; // 1 min
            while (_requests.Count != 0 || !_blockRequestManager.Done())
            {
                var timePassed = TimeUtils.CurrentTimeMillis() - requestTime;
                if (timePassed >= alertTime)
                {
                    Logger.LogWarning($"Waiting to get block batch for too long, time passed: {timePassed / 1000.0} seconds");
                }
                var peer = GetPeer();
                if(!_blockRequestManager.TryGetBatch(out var fromBlock, out var toBlock))
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
                Logger.LogInformation($"Preparing blocks request to send to peer {peer!._publicKey.ToHex()}");
                var myRequestState = new RequestState(RequestType.BlocksRequest, fromBlock, toBlock, peer!);
                HandleRequest(myRequestState);
                requestTime = TimeUtils.CurrentTimeMillis();
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

        private Peer GetPeer()
        {
            var start = TimeUtils.CurrentTimeMillis();
            ulong alertTime = 60 * 1000; // 1 min
            while (true)
            {
                var timePassed = TimeUtils.CurrentTimeMillis() - start;
                if (timePassed >= alertTime)
                {
                    Logger.LogWarning($"Waiting to get peer for too long, time passed: {timePassed / 1000.0} seconds");
                }
                if (!_peerManager.TryGetPeer(out var peer))
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

        public void ResetCheckpointInfo()
        {
            _checkpointBlock = null;
            _checkpointStateHashes = null;
        }

    }
}
