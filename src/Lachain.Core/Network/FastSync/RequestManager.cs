/*
    It provides the downloader which nodes to download and also processes the downloaded nodes.
*/

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

namespace Lachain.Core.Network.FastSync
{
    class RequestManager
    {
 //       private Queue<string> _queue = new Queue<string>();
        private NodeStorage _nodeStorage;

        //how many nodes we should ask for in every request
        private uint _batchSize = 500;

        HybridQueue _hybridQueue;

        public RequestManager(NodeStorage nodeStorage, HybridQueue hybridQueue)
        {
            _nodeStorage = nodeStorage;
            _hybridQueue = hybridQueue;
        }

        public bool TryGetHashBatch(out List<string> hashBatch)
        {
            hashBatch = new List<UInt256>();
            batchId = new List<ulong>();
            lock (this)
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
        
        // it is used to check if downloading the current tree is complete
        public bool Done()
        {
            return _hybridQueue.Complete();
        }

        //useful for debugging purpose, use it to check if some tree is downloaded properly
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

        // "response" is received from a peer for the "hashBatch" of nodes
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void HandleResponse(List<UInt256> hashBatch, List<ulong> batchId, List<TrieNodeInfo> response, ECDSAPublicKey? peer)
        {
            List<string> successfulHashes = new List<string>();
            
            List<string> failedHashes = new List<string>();
            
            List<JObject> successfulNodes = new List<JObject>();

            //discard the whole batch if we haven't got response for all the nodes(or more response, shouldn't happen though)
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
                    //check if actually the node's content produce desired hash or data is corrupted
                    if (_nodeStorage.IsConsistent(node) && hash == (string)node["Hash"])
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

                        //for internal node, we need to extract it's children and put it in the queue for downloading them next
                        if (nodeType.Equals("0x1")) 
                        {
                            var jsonChildren = (JArray)node["ChildrenHash"];
                            foreach (var jsonChild in jsonChildren)
                            {
                                _hybridQueue.Add((string)jsonChild);
                            }
                        }

                        //sending the node to nodeStorage for inserting it to database
                        //(maybe temporarily it will reside in memory but flushed to db later)
                        bool res = _nodeStorage.TryAddNode(node);
                        //informing the hybridQueue that node is received successfully
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
