/*
    It provides the downloader which nodes to download and also processes the downloaded nodes.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Lachain.Core.RPC.HTTP.Web3;
using Lachain.Proto;
using Lachain.Storage.Trie;
using Lachain.Utility.Utils;

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

        public bool TryGetHashBatch(out List<UInt256> hashBatch)
        {
            hashBatch = new List<UInt256>();
            batchId = new List<ulong>();
            lock (this)
            {
                UInt256? hash;
                while(hashBatch.Count < _batchSize && _hybridQueue.TryGetValue(out hash) )
                {
                //    Console.WriteLine("In request manager: got hash: "+ hash);
                    hashBatch.Add(hash!);
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
                    switch (trieNode)
                    {
                        case InternalNode node:
                            foreach(var childId in node.Children)
                            {
                                Console.WriteLine("Enqueueing child: "+ childId);
                                queue.Enqueue(childId);
                            }
                            break;
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
            List<UInt256> successfulHashes = new List<UInt256>();
            
            List<UInt256> failedHashes = new List<UInt256>();
            
            List<TrieNodeInfo?> successfulNodes = new List<TrieNodeInfo?>();

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
                    var node = response[i];
                    //check if actually the node's content produce desired hash or data is corrupted
                    if (_nodeStorage.IsConsistent(node, out var nodeHash) && hash == nodeHash)
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
                        switch (node!.MessageCase)
                        {
                            //for internal node, we need to extract it's children and put it in the queue for downloading them next
                            case TrieNodeInfo.MessageOneofCase.InternalNodeInfo:
                                var childrenHashes = node.InternalNodeInfo.ChildrenHash.ToList();
                                foreach (var childHash in childrenHashes)
                                {
                                    _hybridQueue.Add(childHash);
                                }
                                break;
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
        public void AddHash(UInt256 hash)
        {
            lock(_hybridQueue)
            {
                _hybridQueue.Add(hash);
            }
        }
    }
}
