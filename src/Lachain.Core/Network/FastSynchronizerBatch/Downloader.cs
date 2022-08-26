/*
    Most crucial for downloading the 6 state tries. It is written in a way that we can download different batch of nodes
    concurrently(with asynchronous programming).
*/

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lachain.Core.Blockchain.Checkpoints;
using Lachain.Core.Blockchain.Error;
using Lachain.Logger;
using Lachain.Networking;
using Lachain.Proto;
using Lachain.Utility.Utils;


namespace Lachain.Core.Network.FastSynchronizerBatch
{
    public class Downloader : IDownloader
    {

        private readonly INetworkManager _networkManager;
        private readonly PeerManager _peerManager;
        private readonly IRequestManager _requestManager;
        private readonly IFastSyncRepository _repository;
        private readonly UInt256 EmptyHash = UInt256Utils.Zero;
        private const int DefaultTimeout = 5 * 1000; // 5000 millisecond 
        private readonly IBlockRequestManager _blockRequestManager; 
        // Any request that has been made resides in _requests dictionary. Mapping is requestId -> request.
        private IDictionary<ulong, RequestState> _requests = new Dictionary<ulong, RequestState>();
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
                    _peerManager.TryFreePeer(peer!);
                    Thread.Sleep(500);
                    continue;
                }
                Logger.LogInformation($"Preparing nodes request to send to peer {peer!._publicKey.ToHex()}");
                var myRequestState = new RequestState(RequestType.NodesRequest, hashBatch, batchId, peer!);
                HandleRequest(myRequestState);
                requestTime = TimeUtils.CurrentTimeMillis();
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
                _requests[request._requestId] = request;
                NetworkMessage? message = null;
                switch (request._type)
                {
                    case RequestType.BlocksRequest:
                        message = _networkManager.MessageFactory.BlockBatchRequest(
                            request._fromBlock!.Value, request._toBlock!.Value, request._requestId);
                        break;

                    case RequestType.NodesRequest:
                        message = _networkManager.MessageFactory.TrieNodeByHashRequest(request._nodeBatch!, request._requestId);
                        break;

                    case RequestType.CheckpointStateHashRequest:
                        message = _networkManager.MessageFactory.RootHashByTrieNameRequest(
                            request._blockNumber!.Value, request._trieName!, request._requestId);
                        break;

                    case RequestType.CheckpointBlockRequest:
                        message = _networkManager.MessageFactory.CheckpointBlockRequest(
                            request._blockNumber!.Value, request._requestId);
                        break;

                    default:
                        Logger.LogWarning($"No implementation for request type: {request._type}");
                        throw new Exception($"Invalid request type: {request._type}");
                }
                if (!(message is null))
                {
                    Logger.LogInformation($"Object ready for sending to peer{request._peer._publicKey.ToHex()}, with request id:"
                    + $" {request._requestId}, spent time:{(DateTime.Now - request._start).TotalMilliseconds}");
                    _networkManager.SendTo(request._peer._publicKey, message);
                    Task.Factory.StartNew(() =>
                    {
                        TimeOut(request._peerHasReply, request._requestId);
                    }, TaskCreationOptions.LongRunning);
                }
                else
                {
                    Logger.LogWarning($"Unsupported request {request._type}");
                    throw new Exception($"Invalid request type: {request._type}");
                }
            }
            catch (Exception exception)
            {
                Logger.LogWarning($"Exception raised while trying to send request {request._type} "
                    + $"to peer {request._peer._publicKey.ToHex()} : {exception}");
                _requests.Remove(request._requestId);
                if(_peerManager.TryFreePeer(request._peer, false))
                {
                    switch (request._type)
                    {
                        case RequestType.BlocksRequest:
                            _blockRequestManager.HandleResponse(
                                request._fromBlock!.Value, request._toBlock!.Value, new List<Block>(), request._peer._publicKey);
                            break;

                        case RequestType.NodesRequest:
                            _requestManager.HandleResponse(request._nodeBatch!, request._batchId!, new List<TrieNodeInfo>(), request._peer._publicKey);
                            break;

                        case RequestType.CheckpointBlockRequest:
                            DownloadCheckpointBlock(request._blockNumber!.Value);
                            break;

                        case RequestType.CheckpointStateHashRequest:
                            DownloadCheckpointStateHash(request._blockNumber!.Value, request._trieName!);
                            break;

                        default:
                            Logger.LogWarning($"No implementation for request type: {request._type}");
                            break;
                    }
                }
            }
        }

        private void TimeOut(object peerHasReply, ulong requestId)
        {
            lock (peerHasReply)
            {
                bool gotReply = Monitor.Wait(peerHasReply, TimeSpan.FromMilliseconds(DefaultTimeout));
                // Abort the request if the timer fires.
                if (!gotReply && _requests.TryGetValue(requestId, out var request))
                {
                    _requests.Remove(requestId);
                    if (!(request is null))
                    {
                        var peer = request._peer;
                        TimeSpan time = DateTime.Now - request._start; 
                        Logger.LogWarning($"timed out from peer {peer._publicKey.ToHex()} spent {time.TotalMilliseconds}");
                        _peerManager.TryFreePeer(peer, false);
                        switch (request._type)
                        {
                            case RequestType.NodesRequest:
                                Task.Factory.StartNew(() =>
                                {
                                    _requestManager.HandleResponse(
                                        request._nodeBatch!, request._batchId!, new List<TrieNodeInfo>(), request._peer._publicKey);
                                }, TaskCreationOptions.LongRunning);
                                break;

                            case RequestType.BlocksRequest:
                                Task.Factory.StartNew(() =>
                                {
                                    _blockRequestManager.HandleResponse(
                                        request._fromBlock!.Value, request._toBlock!.Value, new List<Block>(), request._peer._publicKey);
                                }, TaskCreationOptions.LongRunning);
                                break;

                            case RequestType.CheckpointBlockRequest:
                                Task.Factory.StartNew(() =>
                                {
                                    DownloadCheckpointBlock(request._blockNumber!.Value);
                                }, TaskCreationOptions.LongRunning);
                                break;

                            case RequestType.CheckpointStateHashRequest:
                                Task.Factory.StartNew(() =>
                                {
                                    DownloadCheckpointStateHash(request._blockNumber!.Value, request._trieName!);
                                }, TaskCreationOptions.LongRunning);
                                break;

                            default:
                                Logger.LogWarning($"TimeOut not implemented for request: {request._type}");
                                break;
                        }
                    }
                }
            }
        }

        public void HandleBlocksFromPeer(List<Block> response, ulong requestId, ECDSAPublicKey publicKey)
        {
            if (_requests.TryGetValue(requestId, out var request))
            {
                Logger.LogTrace("HandleBlocksFromPeer");
                lock (request._peerHasReply)
                {
                    _requests.Remove(requestId);
                    TimeSpan time = DateTime.Now - request._start;
                    DateTime receiveTime = DateTime.Now;
                    var peer = request._peer;
                    var fromBlock = request._fromBlock;
                    var toBlock = request._toBlock;
                    // Let the TimeOut know that we got the response
                    Monitor.PulseAll(request._peerHasReply);
                
                    try
                    {
                        if (!peer._publicKey.Equals(publicKey) || request._type != RequestType.BlocksRequest)
                        {
                            Logger.LogWarning($"Asked for blocks to peer: {peer._publicKey.ToHex()} with  request id: "
                                + $"{request._requestId} and request type: {request._type}, got reply from peer: {publicKey.ToHex()}");
                            if (!peer._publicKey.Equals(publicKey))
                            {
                                Logger.LogWarning($"Sent to {peer._publicKey.ToHex()}, but got reply from {publicKey.ToHex()}");
                            }
                            if (request._type != RequestType.BlocksRequest)
                            {
                                Logger.LogWarning($"Got request type: {request._type} instead of {RequestType.BlocksRequest}");
                            }
                            throw new Exception($"Invalid reply from peer: {publicKey.ToHex()}");
                        }
                        Logger.LogInformation($"Received data {request._type} size: {BlockRequestManager.ExpectedBlockCount(fromBlock!.Value, toBlock!.Value)}"
                            + $" time spent:{time.TotalMilliseconds} from peer:{peer._publicKey.ToHex()}, preparation time: "
                            + $"{(DateTime.Now-receiveTime).TotalMilliseconds}");
                        _peerManager.TryFreePeer(peer, true);
                        Task.Factory.StartNew(() =>
                        {
                            _blockRequestManager.HandleResponse(fromBlock.Value, toBlock.Value, response, publicKey);
                        }, TaskCreationOptions.LongRunning);
                    }
                    catch (Exception exception)
                    {
                        Logger.LogWarning($"Exception raised while handling blocks from peer: {publicKey.ToHex()} : {exception}");
                        Logger.LogWarning($"Wasted time:{time.TotalMilliseconds} from peer:{peer._publicKey.ToHex()}");
                        _peerManager.TryFreePeer(peer, false);
                        Task.Factory.StartNew(() =>
                        {
                            _blockRequestManager.HandleResponse(fromBlock!.Value, toBlock!.Value, new List<Block>(), publicKey);
                        }, TaskCreationOptions.LongRunning);
                    }
                }
            }
        }

        public void HandleNodesFromPeer(List<TrieNodeInfo> response, ulong requestId, ECDSAPublicKey publicKey)
        {
            if (_requests.TryGetValue(requestId, out var request))
            {
                Logger.LogTrace("HandleNodesFromPeer");
                lock (request._peerHasReply)
                {
                    _requests.Remove(requestId);
                    TimeSpan time = DateTime.Now - request._start;
                    DateTime receiveTime = DateTime.Now;
                    var peer = request._peer;
                    // Let the TimeOut know that we got the response
                    Monitor.PulseAll(request._peerHasReply);
                
                    try
                    {
                        if (!peer._publicKey.Equals(publicKey) || request._type != RequestType.NodesRequest) 
                        {
                            Logger.LogWarning($"Asked for nodes to peer: {peer._publicKey.ToHex()} with  request id: "
                                + $"{request._requestId} and request type: {request._type}, got reply from peer: {publicKey.ToHex()}");
                            if (!peer._publicKey.Equals(publicKey))
                            {
                                Logger.LogWarning($"Sent to {peer._publicKey.ToHex()}, but got reply from {publicKey.ToHex()}");
                            }
                            if (request._type != RequestType.NodesRequest)
                            {
                                Logger.LogWarning($"Got request type: {request._type} instead of {RequestType.NodesRequest}");
                            }
                            throw new Exception($"Invalid reply from peer: {publicKey.ToHex()}");
                        }
                        Logger.LogInformation($"Received data {request._type} size:{request._nodeBatch!.Count}  time spent:{time.TotalMilliseconds}"
                            + $" from peer:{peer._publicKey.ToHex()}, preparation time:{(DateTime.Now-receiveTime).TotalMilliseconds}");
                        _peerManager.TryFreePeer(peer, true);
                        Task.Factory.StartNew(() =>
                        {
                            _requestManager.HandleResponse(request._nodeBatch!, request._batchId!, response, request._peer._publicKey);
                        }, TaskCreationOptions.LongRunning);
                    }
                    catch (Exception exception)
                    {
                        Logger.LogWarning($"Exception raised while handling nodes from peer: {publicKey.ToHex()} : {exception}");
                        Logger.LogWarning($"Wasted time:{time.TotalMilliseconds} from peer:{peer._publicKey.ToHex()}");
                        _peerManager.TryFreePeer(peer, false);
                        Task.Factory.StartNew(() =>
                        {
                            _requestManager.HandleResponse(
                                request._nodeBatch!, request._batchId!, new List<TrieNodeInfo>(), request._peer._publicKey);
                        }, TaskCreationOptions.LongRunning);
                    }
                }
            }
        }

        public void HandleCheckpointBlockFromPeer(Block? block, ulong requestId, ECDSAPublicKey publicKey)
        {
            if (_requests.TryGetValue(requestId, out var request))
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
        }

        public void HandleCheckpointStateHashFromPeer(UInt256? rootHash, ulong requestId, ECDSAPublicKey publicKey)
        {
            if (_requests.TryGetValue(requestId, out var request))
            {
                Logger.LogTrace("HandleCheckpointStateHashFromPeer");
                lock (request._peerHasReply)
                {
                    _requests.Remove(requestId);
                    TimeSpan time = DateTime.Now - request._start;
                    DateTime receiveTime = DateTime.Now;
                    var peer = request._peer;
                    var blockNumber = request._blockNumber;
                    var trieName = request._trieName;
                    // Let the TimeOut know that we got the response
                    Monitor.PulseAll(request._peerHasReply);
                
                    try
                    {
                        if (rootHash is null || !peer._publicKey.Equals(publicKey) || request._type != RequestType.CheckpointStateHashRequest) 
                        {
                            Logger.LogWarning($"Asked for checkpoint state hash for block {blockNumber} and snapshot {trieName} to peer: "
                                + $"{peer._publicKey.ToHex()} with request id: {request._requestId} and request type: {request._type}, "
                                + $"got reply from peer: {publicKey.ToHex()}");
                            if (rootHash is null) Logger.LogWarning($"Found null root hash for {trieName}");
                            if (!peer._publicKey.Equals(publicKey))
                            {
                                Logger.LogWarning($"Sent to {peer._publicKey.ToHex()}, but got reply from {publicKey.ToHex()}");
                            }
                            if (request._type != RequestType.CheckpointStateHashRequest)
                            {
                                Logger.LogWarning($"Got request type: {request._type} instead of {RequestType.CheckpointStateHashRequest}");
                            }
                            throw new Exception($"Invalid reply from peer: {publicKey.ToHex()}");
                        }
                        Logger.LogInformation($"Received data {request._type} time spent:{time.TotalMilliseconds}"
                            + $" from peer:{peer._publicKey.ToHex()}, preparation time:{(DateTime.Now-receiveTime).TotalMilliseconds}");
                        
                        _peerManager.TryFreePeer(peer, true);
                        if (_checkpointStateHashes is null)
                            _checkpointStateHashes = new List<(UInt256, CheckpointType)>();
                        var checkpointType = CheckpointUtils.GetCheckpointTypeForSnapshotName(trieName!);
                        Logger.LogInformation($"Fetched checkpoint state hash for {trieName} successfully");
                        _checkpointStateHashes.Add((rootHash, checkpointType!.Value));
                    }
                    catch (Exception exception)
                    {
                        Logger.LogWarning($"Exception raised while handling nodes from peer: {publicKey.ToHex()} : {exception}");
                        Logger.LogWarning($"Wasted time:{time.TotalMilliseconds} from peer:{peer._publicKey.ToHex()}");
                        _peerManager.TryFreePeer(peer, false);
                        // Try again
                        Task.Factory.StartNew(() =>
                        {
                            DownloadCheckpointStateHash(request._blockNumber!.Value, request._trieName!);
                        }, TaskCreationOptions.LongRunning);
                    }
                }
            }
        }

        public void DownloadBlocks()
        {

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
                    _peerManager.TryFreePeer(peer!);
                    Thread.Sleep(500);
                    continue;
                }
                Logger.LogInformation($"Preparing blocks request to send to peer {peer!._publicKey.ToHex()}");
                var myRequestState = new RequestState(RequestType.BlocksRequest, fromBlock, toBlock, peer!);
                HandleRequest(myRequestState);
                requestTime = TimeUtils.CurrentTimeMillis();
            }
        }

        private void DownloadCheckpointBlock(ulong blockNumber)
        {
            Logger.LogTrace($"Trying to download checkpoint block {blockNumber}");
            var peer = GetPeer();
            var request = new RequestState(RequestType.CheckpointBlockRequest, blockNumber, peer);
            HandleRequest(request);
        }

        private void DownloadCheckpointStateHash(ulong blockNumber, string trieName)
        {
            Logger.LogTrace($"Trying to download checkpoint state hash for block {blockNumber} and snapshot {trieName}");
            var peer = GetPeer();
            var request = new RequestState(RequestType.CheckpointStateHashRequest, blockNumber, trieName, peer);
            HandleRequest(request);
        }

        public void DownloadCheckpoint(ulong blockNumber, string[] trieNames)
        {
            DownloadCheckpointBlock(blockNumber);
            foreach (var trieName in trieNames)
            {
                DownloadCheckpointStateHash(blockNumber, trieName);
            }
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
                    Thread.Sleep(200);
                    continue;
                }
                return peer!;
            }
        }

        public void ResetCheckpointInfo()
        {
            _checkpointBlock = null;
            _checkpointStateHashes = null;
        }

    }
}