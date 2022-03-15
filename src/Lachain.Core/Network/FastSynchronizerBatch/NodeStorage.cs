/*
    We don't add to db everytime we get a node, we accumulate data and make write in batches. NodeStorage helps to hold data 
    temporary in memory and periodically flushes it to database.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Lachain.Storage;
using Lachain.Storage.Trie;
using Lachain.Storage.State;
using Lachain.Utility.Utils;
using Lachain.Core.RPC.HTTP.Web3;
using Lachain.Crypto;
using Lachain.Utility.Serialization;
using Lachain.Proto;


namespace Lachain.Core.Network.FastSynchronizerBatch
{
    class NodeStorage
    {
        private IDictionary<string, ulong> _idCache = new ConcurrentDictionary<string, ulong>();
        private uint _idCacheCapacity = 100000;

        private IDictionary<ulong, IHashTrieNode> _nodeCache = new ConcurrentDictionary<ulong, IHashTrieNode>();
        private uint _nodeCacheCapacity = 5000;
        private IRocksDbContext _dbContext;
        private VersionFactory _versionFactory;
        private const string EmptyHash = "0x0000000000000000000000000000000000000000000000000000000000000000";
        private NodeRetrieval nodeRetrieval;
        public NodeStorage(IRocksDbContext dbContext, VersionFactory versionFactory)
        {
            _dbContext = dbContext;
            _versionFactory = versionFactory;
            nodeRetrieval = new NodeRetrieval(_dbContext);
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryAddNode(JObject jsonNode)
        {
            string nodeHash = (string)jsonNode["Hash"];
            if (nodeHash == null) return false;
            byte[] nodeHashBytes = HexUtils.HexToBytes(nodeHash);
            bool foundId = GetIdByHash(nodeHash, out ulong id);
            IHashTrieNode? trieNode;
            if(foundId)
            {
                bool foundNode = TryGetNode(id, out trieNode);
                if(!foundNode) trieNode = BuildHashTrieNode(jsonNode);
            }
            else trieNode = BuildHashTrieNode(jsonNode);
            _nodeCache[id] = trieNode;
            if(_nodeCache.Count>=_nodeCacheCapacity) Commit();
            return false;
        }

        public bool ExistNode(string nodeHash)
        {
            if(GetIdByHash(nodeHash, out ulong id))
            {
                return TryGetNode(id, out IHashTrieNode? node);
            }
            return false;
        }

        public bool GetIdByHash(string nodeHash, out ulong id)
        {
            id = 0;
            if(nodeHash.Equals(EmptyHash)) return true;
            if(_idCache.TryGetValue(nodeHash, out id)) return true;
            var idByte = _dbContext.Get(EntryPrefix.VersionByHash.BuildPrefix(HexUtils.HexToBytes(nodeHash)));
            if(!(idByte is null)){
                id = UInt64Utils.FromBytes(idByte);
                return true;
            }
            id = _versionFactory.NewVersion();
            _idCache[nodeHash] = id;
            if(_idCache.Count>=_idCacheCapacity) CommitIds();
            return false;
        }


        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryGetNode(ulong id, out IHashTrieNode? trieNode)
        {
            if(_nodeCache.TryGetValue(id, out trieNode)) return true;
            var rawNode = _dbContext.Get(EntryPrefix.PersistentHashMap.BuildPrefix(id));
            trieNode = null;
            if (rawNode == null) return false; 
            trieNode = NodeSerializer.FromBytes(rawNode);
            return true; 
        }
        public bool IsConsistent(JObject node)
        {
            if(node is null || node.Count==0) return false;
            if(((string)node["NodeType"]).Equals("0x1"))
            {
                uint mask = Convert.ToUInt32((string)node["ChildrenMask"], 16);
                List<byte[]> childrenHashes = new List<byte[]>();
                var jsonChildrenHash = (JArray)node["ChildrenHash"];
                foreach(var jsonChildHash in jsonChildrenHash)
                {
                    string childHash = (string)jsonChildHash;
                    childrenHashes.Add(HexUtils.HexToBytes(childHash));
                }
                byte[] hash = childrenHashes
                .Zip(InternalNode.GetChildrenLabels(mask), (bytes, i) => new[] {i}.Concat(bytes))
                .SelectMany(bytes => bytes)
                .KeccakBytes();
                return ((string)node["Hash"]).Equals(HexUtils.ToHex(hash));
            }
            else{
                byte[] keyHash = HexUtils.HexToBytes(((string)node["KeyHash"]));
                byte[] value = HexUtils.HexToBytes(((string)node["Value"]));
                byte[] hash = keyHash.Length.ToBytes().Concat(keyHash).Concat(value).KeccakBytes(); 
                return ((string)node["Hash"]).Equals(HexUtils.ToHex(hash));
            }
        }

        public IHashTrieNode BuildHashTrieNode(JObject jsonNode)
        {
            IHashTrieNode? trieNode;
            if(((string)jsonNode["NodeType"]).Equals("0x1") == true)
            {
                var jsonChildrenHash = (JArray)jsonNode["ChildrenHash"];
                List<byte[]> childrenHash = new List<byte[]>();
                List<ulong> children = new List<ulong>();
                foreach(var jsonChildHash in jsonChildrenHash)
                {
                    string childHash = (string)jsonChildHash;
                    childrenHash.Add(HexUtils.HexToBytes(childHash));
                    bool foundId = GetIdByHash(childHash, out ulong childId);
                    children.Add(childId);
                }
                uint mask = Convert.ToUInt32((string)jsonNode["ChildrenMask"], 16);
                trieNode = new InternalNode(mask, children, childrenHash);
            }
            else
            {
                byte[] keyHash = HexUtils.HexToBytes((string)jsonNode["KeyHash"]);
                byte[] value = HexUtils.HexToBytes((string)jsonNode["Value"]);
                trieNode = new LeafNode(keyHash, value);
            }
            return trieNode;
        }

        public void AddBlock(IBlockSnapshot blockSnapshot, string blockRawHex)
        {
            byte[] raw = HexUtils.HexToBytes(blockRawHex);
            Block block = Block.Parser.ParseFrom(raw);
            blockSnapshot.AddBlock(block);
            if(block.Header.Index%200==0) Console.WriteLine("Added BlockHeader: "+ block.Header.Index);
            if(block.Header.Index%10000==0) blockSnapshot.Commit();
        }

        public void CommitNodes()
        {
            RocksDbAtomicWrite tx = new RocksDbAtomicWrite(_dbContext);
            foreach(var item in _nodeCache)
            {
                tx.Put(EntryPrefix.PersistentHashMap.BuildPrefix(item.Key), NodeSerializer.ToBytes(item.Value));
          //      Console.WriteLine("Adding node to DB : "+item.Key);
            }
            tx.Commit();
            _nodeCache.Clear();
        }
        public void CommitIds()
        {
            RocksDbAtomicWrite tx = new RocksDbAtomicWrite(_dbContext);
            foreach(var item in _idCache)
            {
                tx.Put(EntryPrefix.VersionByHash.BuildPrefix(HexUtils.HexToBytes(item.Key)), UInt64Utils.ToBytes(item.Value));
            }
            ulong nextVersion = _versionFactory.CurrentVersion + 1;
            tx.Put(EntryPrefix.StorageVersionIndex.BuildPrefix((uint) RepositoryType.MetaRepository), nextVersion.ToBytes().ToArray());
            tx.Commit();
            _idCache.Clear();
        }

        public void Commit()
        {
            CommitIds();
            CommitNodes();
        }
    }
}
