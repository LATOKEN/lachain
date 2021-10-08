using System.Collections.Generic;
using System.Text;
using System.Net;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lachain.Core.Network.FastSynchronizerBatch
{
    class RequestState
    {
        // This class stores the State of the request.
        const int BUFFER_SIZE = 1024 * 128;
        public StringBuilder requestData;
        public byte[] BufferRead;
        public HttpWebRequest request;
        public HttpWebResponse response;
        public Stream streamResponse;
        public Peer peer;
        public List<string> batch;
        public uint type;
        public DateTime start;
        public RequestState()
        {
            BufferRead = new byte[BUFFER_SIZE];
            requestData = new StringBuilder("");
            request = null;
            streamResponse = null;
            peer = null;
            batch = null;
            type = 1;
        }
    }
}
