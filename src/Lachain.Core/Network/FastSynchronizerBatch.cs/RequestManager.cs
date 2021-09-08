using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Lachain.Core.Network.FastSynchronizerBatch
{
    class RequestManager
    {
        private Queue<string> _queue = new Queue<string>();
        private HashSet<string> _pending = new HashSet<string>();
        private NodeStorage _nodeStorage;
        private uint _batchSize = 20;

        public RequestManager(NodeStorage nodeStorage)
        {
            _nodeStorage = nodeStorage;
        }

        public bool TryGetHashBatch(out List<string> hashBatch)
        {
            hashBatch = new List<string>();
            lock(this)
            {
                while(_queue.Count > 0 && hashBatch.Count < _batchSize)
                {
                    var hash = _queue.Dequeue();
                    hashBatch.Add(hash);
                    _pending.Add(hash);
                }
            }
            if (hashBatch.Count == 0) return false;

            return true;
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Done()
        {
            return _queue.Count == 0 && _pending.Count == 0;
        }

        public bool CheckConsistency(ulong rootId)
        {
            Queue<ulong> queue = new Queue<ulong>();
            queue.Enqueue(rootId);
            while(queue.Count > 0)
            {
                ulong cur = queue.Dequeue();
                System.Console.WriteLine("id: " + cur);
            //    System.Console.WriteLine($"id: {_nodeStorage.GetIdByHash(cur)}");
                if(_nodeStorage.TryGetNode(cur, out var node))
                {
                //    Console.WriteLine("printing Node");
                //    Console.WriteLine(node);
                    var nodeType = (string)node["NodeType"];
            //        if (nodeType == null) return false; 

                    if (nodeType.Equals("0x1")) // internal node 
                    {
                        var jsonChildren = (JArray)node["Children"];
                        foreach (var jsonChild in jsonChildren)
                        {
                            ulong childId = Convert.ToUInt64((string)jsonChild,16);
                            Console.WriteLine("Enqueueing child: "+ childId);
                            queue.Enqueue(childId);
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Not Found: " + cur);
                }
            }
            return true;
        }

        public void HandleResponse(List<string> hashBatch, JArray response)
        {
/*
            Console.WriteLine("hashes in hashBatch printing started");
            foreach(var hash in hashBatch){
                Console.WriteLine(hash);
            }
            Console.WriteLine("response printing");
            Console.WriteLine(response); 
*/

            List<string> successfulHashes = new List<string>();
            
            List<string> failedHashes = new List<string>();
            
            List<JObject> successfulNodes = new List<JObject>();

            if (hashBatch.Count != response.Count)
            {
                failedHashes = hashBatch;
            }
            else
            {
                for (var i = 0; i < hashBatch.Count; i++)
                {
                    var hash = hashBatch[i];
                    JObject node = (JObject)response[i];
                    if (_nodeStorage.IsConsistent(node))
                    {
                        successfulHashes.Add(hash);
                        successfulNodes.Add(node);
                    }
                    else
                    {
                        failedHashes.Add(hash);
                    }
                }
            }
/*            Console.WriteLine("success Hash");
            foreach(var hash in successfulHashes){
                Console.WriteLine(hash);
            }
            Console.WriteLine("Failed Hash");
            foreach(var hash in failedHashes){
                Console.WriteLine(hash);
            } */
            lock (this)
            {
                foreach (var hash in failedHashes)
                {
                    if (!_pending.TryGetValue(hash, out var foundHash) || !hash.Equals(foundHash))
                    {
                        // do nothing, this request was probably already served
                    }
                    else
                    {
                        _pending.Remove(hash);
                        _queue.Enqueue(hash);
                    }
                }


                for(var i = 0; i < successfulHashes.Count; i++)
                {
                    var hash = successfulHashes[i];
                    var node = successfulNodes[i];
                    if (!_pending.TryGetValue(hash, out var foundHash) || !hash.Equals(foundHash))
                    {
                        // do nothing, this request was probably already served
                    }
                    else
                    {
                        bool res = _nodeStorage.TryAddNode(node);
                        _pending.Remove(hash);

                        var nodeType = (string)node["NodeType"];
                        if (nodeType == null) continue;

                        if (nodeType.Equals("0x1")) // internal node 
                        {
                            var jsonChildren = (JArray)node["ChildrenHash"];
                            foreach (var jsonChild in jsonChildren)
                            {
                                _queue.Enqueue((string)jsonChild);
                            }
                        }
                    }
                }
            }
        }
        public void AddHash(string hash)
        {
            lock(_queue)
            {
                _queue.Enqueue(hash);
            }
        }
    }
}
