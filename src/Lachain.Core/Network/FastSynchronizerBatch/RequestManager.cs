/*
    It provides the downloader which nodes to download and also processes the downloaded nodes.
*/

using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Storage.Trie;


namespace Lachain.Core.Network.FastSynchronizerBatch
{
    public class RequestManager : IRequestManager
    {
        private static readonly ILogger<RequestManager>
            Logger = LoggerFactory.GetLoggerForClass<RequestManager>();
        private readonly IFastSyncRepository _repository;

        // how many nodes we should ask for in every request
        private uint _batchSize = 500;

        private readonly IHybridQueue _hybridQueue;

        public RequestManager(IFastSyncRepository repository, IHybridQueue hybridQueue)
        {
            _repository = repository;
            _hybridQueue = hybridQueue;
        }

        public bool TryGetHashBatch(out List<UInt256> hashBatch)
        {
            hashBatch = new List<UInt256>();
            lock(this)
            {
                UInt256? hash;
                while (hashBatch.Count < _batchSize && _hybridQueue.TryGetValue(out hash))
                {
                    hashBatch.Add(hash!);
                }
            }
            if (hashBatch.Count == 0)
            {
                return false;
            }

            return true;
        }
        
        // it is used to check if downloading the current tree is complete
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Done()
        {
            return _hybridQueue.Complete();
        }

        // useful for debugging purpose, use it to check if some tree is downloaded properly
        public bool CheckConsistency(ulong rootId)
        {
            if(rootId == 0) return true;
            Queue<ulong> queue = new Queue<ulong>();
            queue.Enqueue(rootId);
            while(queue.Count > 0)
            {
                ulong cur = queue.Dequeue();
                Logger.LogTrace("id: " + cur);
                if(_repository.TryGetNode(cur, out IHashTrieNode? trieNode))
                {
                    switch (trieNode)
                    {
                        case InternalNode node:
                            foreach(var childId in node.Children)
                            {
                                Logger.LogTrace("Enqueueing child: "+ childId);
                                queue.Enqueue(childId);
                            }
                            break;
                    }
                }
                else
                {
                    Logger.LogTrace("Not Found: " + cur);
                    return false;
                }
            }
            return true;
        }

        // "response" is received from a peer for the "hashBatch" of nodes
        public void HandleResponse(List<UInt256> hashBatch, List<TrieNodeInfo> response)
        {
            List<UInt256> successfulHashes = new List<UInt256>();
            
            List<UInt256> failedHashes = new List<UInt256>();
            
            List<TrieNodeInfo> successfulNodes = new List<TrieNodeInfo>();

            // discard the whole batch if we haven't got response for all the nodes(or more response, shouldn't happen though)
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
                    // check if actually the node's content produce desired hash or data is corrupted
                    if (_repository.IsConsistent(node, out var nodeHash) && hash == nodeHash)
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
                            // for internal node, we need to extract it's children and put it in the queue for downloading them next
                            case TrieNodeInfo.MessageOneofCase.InternalNodeInfo:
                                var childrenHashes = node.InternalNodeInfo.ChildrenHash.ToList();
                                foreach (var childHash in childrenHashes)
                                {
                                    _hybridQueue.Add(childHash);
                                }
                                break;
                        }

                        // sending the node to repository for inserting it to database
                        // (maybe temporarily it will reside in memory but flushed to db later)
                        bool res = _repository.TryAddNode(node);
                        // informing the hybridQueue that node is received successfully
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
