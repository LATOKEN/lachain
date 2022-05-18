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
        public Peer _peer;
        public List<ulong>? _blockBatch;
        public List<UInt256>? _nodeBatch;
        public ulong? _blockNumber;
        public string? _trieName;
        public RequestType _type;
        public DateTime _start;
        public ulong _requestId;
        public object _peerHasReply = new object();
        public RequestState(RequestType type, List<ulong> blockBatch, Peer peer)
        {
            Initialize();
            _blockBatch = blockBatch;
            _type = type;
            _peer = peer;
        }
        public RequestState(RequestType type, List<UInt256> nodeBatch, Peer peer)
        {
            Initialize();
            _nodeBatch = nodeBatch;
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
