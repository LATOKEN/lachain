using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Lachain.Storage.Trie;
using Lachain.Utility.Utils;
using Lachain.Core.RPC.HTTP.Web3;

namespace Lachain.Core.Network.FastSynchronizerBatch
{
    class RequestManager
    {
 //       private Queue<string> _queue = new Queue<string>();
        private NodeStorage _nodeStorage;
        private uint _batchSize = 200;

        HybridQueue3 _hybridQueue;
        public int maxQueueSize = 0;

        public RequestManager(NodeStorage nodeStorage, HybridQueue3 hybridQueue)
        {
            _nodeStorage = nodeStorage;
            _hybridQueue = hybridQueue;
        }

        public bool TryGetHashBatch(out List<string> hashBatch)
        {
            hashBatch = new List<string>();
            lock(this)
            {
                string hash;
                while(hashBatch.Count < _batchSize && _hybridQueue.TryGetValue(out hash) )
                {
                //    Console.WriteLine("In request manager: got hash: "+ hash);
                    hashBatch.Add(hash);
                }
            }
            if (hashBatch.Count == 0){
            //    Console.WriteLine("could not get hash");
                return false;
            }

            return true;
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Done()
        {
            return _hybridQueue.Complete();
        }

        public bool CheckConsistency(ulong rootId)
        {
            if(rootId==0) return true;
            Queue<ulong> queue = new Queue<ulong>();
            queue.Enqueue(rootId);
            while(queue.Count > 0)
            {
                ulong cur = queue.Dequeue();
                System.Console.WriteLine("id: " + cur);
            //    System.Console.WriteLine($"id: {_nodeStorage.GetIdByHash(cur)}");
                if(_nodeStorage.TryGetNode(cur, out IHashTrieNode? trieNode))
                {
                    JObject node = Web3DataFormatUtils.Web3Node(trieNode);
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
                    return false;
                }
            }
            return true;
        }

        public void HandleResponse(List<string> hashBatch, JArray response)
        {
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
            lock (this)
            {
                foreach (var hash in failedHashes)
                {
                    if (!_hybridQueue.isPending(hash))
                    {
                        // do nothing, this request was probably already served
                    }
                    else
                    {
                        _hybridQueue.Add(hash);   
                    }
                    _hybridQueue.Add(hash);
                }


                for(var i = 0; i < successfulHashes.Count; i++)
                {
                    var hash = successfulHashes[i];
                    var node = successfulNodes[i];
                    if (!_hybridQueue.isPending(hash))
                    {
                        // do nothing, this request was probably already served
                    }
                    else
                    {
                        var nodeType = (string)node["NodeType"];
                        if (nodeType == null) continue;

                        if (nodeType.Equals("0x1")) // internal node 
                        {
                            var jsonChildren = (JArray)node["ChildrenHash"];
                            foreach (var jsonChild in jsonChildren)
                            {
                                _hybridQueue.Add((string)jsonChild);
                            }
                        }

                        bool res = _nodeStorage.TryAddNode(node);
                        _hybridQueue.ReceivedNode(hash);
                    }
                }
            }
        }
        public void AddHash(string hash)
        {
            lock(_hybridQueue)
            {
                _hybridQueue.Add(hash);
            }
        }
    }
}
