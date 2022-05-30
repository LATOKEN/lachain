using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Lachain.Proto;
using Lachain.Utility.Serialization;


namespace Lachain.Core.Network.FastSynchronizerBatch
{
    public class RequestState
    {
        // This class stores the State of the request.

        // Common info for all type of requests
        public Peer _peer;
        public RequestType _type;
        public DateTime _start;
        // _requestId is generated randomly so that we can check if we got the reply that we wanted from peer.
        // If the message from peer to peer cannot be intercepted then this will work.
        public ulong _requestId;
        public object _peerHasReply = new object();
        
        // _fromBlock and _toBlock indicate the range to request blocks in a batch
        public ulong? _fromBlock;
        public ulong? _toBlock;

        // _nodeBatch is the hash of trie nodes in a batch and _batchId is their corresponding batch id
        public List<UInt256>? _nodeBatch;
        public List<ulong>? _batchId;

        // _blockNumber and _trieName are used to fetch checkpoint state hash
        // _blockNumber is also used to fetch checkpoint block.
        public ulong? _blockNumber;
        public string? _trieName;
        public RequestState(RequestType type, ulong fromBlock, ulong toBlock, Peer peer)
        {
            Initialize();
            _fromBlock = fromBlock;
            _toBlock = toBlock;
            _type = type;
            _peer = peer;
        }
        public RequestState(RequestType type, List<UInt256> nodeBatch, List<ulong> batchId, Peer peer)
        {
            Initialize();
            _nodeBatch = nodeBatch;
            _batchId = batchId;
            _type = type;
            _peer = peer;
        }
        public RequestState(RequestType type, ulong blockNumber, Peer peer)
        {
            Initialize();
            _blockNumber = blockNumber;
            _type = type;
            _peer = peer;
        }
        public RequestState(RequestType type, ulong blockNumber, string trieName, Peer peer)
        {
            Initialize();
            _blockNumber = blockNumber;
            _trieName = trieName;
            _type = type;
            _peer = peer;
        }

        private void Initialize()
        {
            _start = DateTime.Now;
            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            var random = new byte[8];
            rng.GetBytes(random);
            _requestId = SerializationUtils.ToUInt64(random);
        }
    }
}
