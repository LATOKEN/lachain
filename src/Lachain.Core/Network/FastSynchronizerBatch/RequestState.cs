using System.Collections.Generic;
using System.Text;
using System.Net;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Proto;

namespace Lachain.Core.Network.FastSynchronizerBatch
{
    public class RequestState
    {
        // This class stores the State of the request.
        public Peer _peer;
        public List<ulong>? _blockBatch;
        public List<UInt256>? _nodeBatch;
        public RequestType _type;
        public DateTime _start;
        public ulong _requestId;
        public object _peerHasReply;
        public RequestState(RequestType type, List<ulong> blockBatch, DateTime time, Peer peer, ulong requestId)
        {
            _blockBatch = blockBatch;
            _type = type;
            _start = time;
            _peer = peer;
            _requestId = requestId;
            _peerHasReply = new object();
        }
        public RequestState(RequestType type, List<UInt256> nodeBatch, DateTime time, Peer peer, ulong requestId)
        {
            _nodeBatch = nodeBatch;
            _type = type;
            _start = time;
            _peer = peer;
            _requestId = requestId;
            _peerHasReply = new object();
        }
    }
}
