using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using System.Runtime.CompilerServices;
using Lachain.Storage;
using Lachain.Storage.Trie;
using Lachain.Utility.Utils;
using Lachain.Core.RPC.HTTP.Web3;

namespace Lachain.Core.Network.FastSynchronizerBatch
{
    class NodeStorage
    {
        private IDictionary<string, JObject> _nodeStorage = new Dictionary<string, JObject>();
        private IRocksDbContext _dbContext;
        private VersionFactory _versionFactory;
        
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
            ulong id = GetIdByHash(nodeHashBytes);
            IHashTrieNode? trieNode = nodeRetrieval.TryGetNode(id);
            if(trieNode is null)
            {
                if(((string)jsonNode["NodeType"]).Equals("0x1") == true)
                {
                    var jsonChildrenHash = (JArray)jsonNode["ChildrenHash"];
                    List<byte[]> childrenHash = new List<byte[]>();
                    foreach(var jsonChildHash in jsonChildrenHash)
                    {
                        childrenHash.Add(HexUtils.HexToBytes((string)jsonChildHash));
                    }
                    uint mask = Convert.ToUInt32((string)jsonNode["ChildrenMask"], 16);
                    List<ulong> children = new List<ulong>();
                    foreach(var childHash in childrenHash)
                    {
                        children.Add(GetIdByHash(childHash));
                    }
                    trieNode = new InternalNode(mask, children, childrenHash);
                }
                else
                {
                    byte[] keyHash = HexUtils.HexToBytes((string)jsonNode["KeyHash"]);
                    byte[] value = HexUtils.HexToBytes((string)jsonNode["Value"]);
                    trieNode = new LeafNode(keyHash, value);
                }
                _dbContext.Save(EntryPrefix.PersistentHashMap.BuildPrefix(id), NodeSerializer.ToBytes(trieNode));
                var rawNode = _dbContext.Get(EntryPrefix.PersistentHashMap.BuildPrefix(id));
                IHashTrieNode node = NodeSerializer.FromBytes(rawNode);

                Console.WriteLine("Node from DB");
                Console.WriteLine(Web3DataFormatUtils.Web3Node(node));
                Console.WriteLine("Node used");
                Console.WriteLine(jsonNode);

                return true;
            }
            return false;
        }

        public ulong GetIdByHash(byte[] nodeHashBytes)
        {
            var idByte = _dbContext.Get(EntryPrefix.VersionByHash.BuildPrefix(nodeHashBytes));
            if(!(idByte is null)) return UInt64Utils.FromBytes(idByte);
            ulong id = _versionFactory.NewVersion();
            _dbContext.Save(EntryPrefix.VersionByHash.BuildPrefix(nodeHashBytes), UInt64Utils.ToBytes(id));
            return id;
        }
        public ulong GetIdByHash(string nodeHash)
        {
            return GetIdByHash(HexUtils.HexToBytes(nodeHash));
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryGetNode(string nodeHash, out JObject node)
        {
            var id = GetIdByHash(nodeHash);
            var rawNode = _dbContext.Get(EntryPrefix.PersistentHashMap.BuildPrefix(id));
            node = null;
            if (rawNode == null) return false; 
            IHashTrieNode trieNode = NodeSerializer.FromBytes(rawNode);
            node = Web3DataFormatUtils.Web3Node(trieNode);
            return true; 
        }

        public bool IsConsistent(JObject node)
        {
            return node != null && node.Count > 0;
        }
    }
}
