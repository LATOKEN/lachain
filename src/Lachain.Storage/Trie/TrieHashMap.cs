using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Lachain.Crypto;
using Lachain.Proto;
using Lachain.Utility.Utils;
using Lachain.Utility.Serialization;
using RocksDbSharp;

namespace Lachain.Storage.Trie
{
    internal class TrieHashMap : ITrieMap
    {
        private readonly IDictionary<ulong, IHashTrieNode> _nodeCache = new ConcurrentDictionary<ulong, IHashTrieNode>();
        private readonly ISet<ulong> _persistedNodes = new HashSet<ulong>();
        const int Capacity = 100000;
        private LRUCache _lruCache = new LRUCache(Capacity);
        private SpinLock _dataLock = new SpinLock();
        
        private readonly NodeRepository _repository;
        private readonly VersionFactory _versionFactory;

        public TrieHashMap(NodeRepository repository, VersionFactory versionFactory)
        {
            _repository = repository;
            _versionFactory = versionFactory;
        }

        private void EnsurePersisted(ulong root, RocksDbAtomicWrite batch)
        {
            if (root == 0) return;
            if (!_nodeCache.TryGetValue(root, out var node) || _persistedNodes.Contains(root))
                return;
            foreach (var child in node.Children)
                EnsurePersisted(child, batch);
            _repository.WriteNodeToBatch(root, node, batch);
            var lockTaken = false;
            _dataLock.Enter(ref lockTaken);
            try
            {
                _persistedNodes.Add(root);
            }
            finally
            {
                if (lockTaken) _dataLock.Exit();
            }
        }

        public void Checkpoint(ulong root, RocksDbAtomicWrite batch)
        {
            EnsurePersisted(root, batch);
            ClearCaches();
        }

        public void ClearCaches()
        {
            _nodeCache.Clear();
            var lockTaken = false;
            _dataLock.Enter(ref lockTaken);
            try
            {
                _persistedNodes.Clear();
            }
            finally
            {
                if (lockTaken) _dataLock.Exit();
            }
        }

        private static byte[] Hash(IEnumerable<byte> key)
        {
            return key.KeccakBytes();
        }

        public ulong Add(ulong root, byte[] key, byte[] value)
        {
            key = Hash(key);
            return AddInternal(root, 0, key, value, true);
        }

        public ulong AddOrUpdate(ulong root, byte[] key, byte[] value)
        {
            key = Hash(key);
            return AddInternal(root, 0, key, value, false);
        }

        public ulong Update(ulong root, byte[] key, byte[] value)
        {
            key = Hash(key);
            return UpdateInternal(root, 0, key, value);
        }

        public ulong Delete(ulong root, byte[] key, out byte[]? value)
        {
            key = Hash(key);
            return DeleteInternal(root, 0, key, out value, true);
        }

        public ulong TryDelete(ulong root, byte[] key, out byte[]? value)
        {
            key = Hash(key);
            return DeleteInternal(root, 0, key, out value, false);
        }

        public byte[]? Find(ulong root, byte[] key)
        {
            key = Hash(key);
            return FindInternal(root, key);
        }

        public IEnumerable<byte[]> GetValues(ulong root)
        {
            return TraverseValues(root);
        }

        public bool CheckAllNodeHashes(ulong root)
        {
            IDictionary<ulong,IHashTrieNode> dict = GetAllNodes(root);
            foreach(var node in dict)
            {
                if( !(node.Value.Hash.SequenceEqual(RecalculateHash(node.Key)))) return false ;
            }
            return true ;
        }

        public IDictionary<ulong,IHashTrieNode> GetAllNodes(ulong root)
        {
            IDictionary<ulong,IHashTrieNode> dict = new ConcurrentDictionary<ulong, IHashTrieNode>();
            TraverseNodes(root,dict);
            return dict;
        }

        public UInt256 GetHash(ulong root)
        {
            return GetNodeById(root)?.Hash?.ToUInt256() ?? UInt256Utils.Zero;
        }

        private IEnumerable<byte[]> TraverseValues(ulong root)
        {
            if (root == 0)
                yield break;

            var node = GetNodeById(root);
            if (node is null) throw new InvalidOperationException("corrupted trie");
            switch (node)
            {
                case LeafNode leafNode:
                    yield return leafNode.Value;
                    break;
                default:
                    foreach (var child in node.Children)
                    foreach (var value in TraverseValues(child))
                        yield return value;
                    break;
            }
        }

        public byte[] RecalculateHash(ulong root)
        {
            var node = GetNodeById(root);
            if(node is null) return new byte[] {};

            switch(node)
            {
                case InternalNode internalNode:
                    List<byte[]>  childrenHashes = new List<byte[]>();
                    foreach(var child in internalNode.Children ) childrenHashes.Add( GetNodeById(child).Hash );

                    return childrenHashes
                    .Zip( InternalNode.GetChildrenLabels(internalNode.ChildrenMask) , (bytes, i) => new[] {i}.Concat(bytes))
                    .SelectMany(bytes => bytes)
                    .KeccakBytes();
                case LeafNode leafNode:
                    return leafNode.KeyHash.Length.ToBytes().Concat(leafNode.KeyHash).Concat(leafNode.Value).KeccakBytes();

             }
            return new byte[] {}; 
        }

        private void TraverseNodes(ulong root, IDictionary<ulong,IHashTrieNode> dict)
        {
            if(root == 0) return;
            var node = GetNodeById(root);
            if (node is null) throw new InvalidOperationException("corrupted trie");
            dict[root] = node;
            
            switch (node)
            {
                case LeafNode leafNode:
                    break ;
                default:
                    foreach (var child in node.Children)
                        TraverseNodes(child,dict);
                    break;
            }
            return;
        }

        private IHashTrieNode? GetNodeById(ulong id)
        {
            if (id == 0) return null;
            if (_nodeCache.TryGetValue(id, out var node)) return node;
            var _node = _lruCache.Get(id);
            if (_node == null)
            {
                _node = _repository.GetNode(id);
                _lruCache.Add(id, _node);
            }
            return _node;
        }

        private ulong ModifyInternalNode(ulong id, InternalNode node, byte h, ulong value, byte[]? valueHash)
        {
            if (value == 0 && node.GetChildByHash(h) != 0 && node.Children.Count() == 2)
            {
                // we have to handle case when one of two children is deleted and internal node is folded to leaf
                var secondChild = node.Children.First(child => child != node.GetChildByHash(h));
                // fold only if secondChild is also a leaf 
                if (GetNodeById(secondChild).Type == NodeType.Leaf)
                {
                    return secondChild;
                }
            }

            if(value!=0 && node.GetChildByHash(h) != 0 && node.Children.Count() == 1 && GetNodeById(value).Type == NodeType.Leaf) return value ;

            var modified = InternalNode.ModifyChildren(
                node, h, value,
                node.Children.Select(id => GetNodeById(id)?.Hash ?? throw new InvalidOperationException()),
                valueHash
            );
            if (modified == null) return 0u;
            var newId = _versionFactory.NewVersion();
            _nodeCache[newId] = modified;
            return newId;
        }

        private ulong NewLeafNode(IEnumerable<byte> key, IEnumerable<byte> value)
        {
            var newId = _versionFactory.NewVersion();
            _nodeCache[newId] = new LeafNode(key, value);
            return newId;
        }

        private ulong UpdateLeafNode(ulong id, LeafNode node, byte[] value)
        {
            if( node.Value.SequenceEqual(value) ) return id ;
            else{
                var newId = NewLeafNode(node.KeyHash, value) ;
                return newId ;
            }
        }

        private static byte HashFragment(IReadOnlyList<byte> hash, int n)
        {
            var bitOffset = 5 * n;
            if (bitOffset % 8 <= 3)
                return (byte) ((hash[bitOffset / 8] >> (bitOffset % 8)) & 0x1F);
            var fromFirst = 8 - bitOffset % 8;
            var first = (uint) (hash[bitOffset / 8] >> (bitOffset % 8));
            var second = hash[bitOffset / 8 + 1] & ((1u << (5 - fromFirst)) - 1);
            return (byte) ((second << fromFirst) | first);
        }

        private ulong SplitLeafNode(
            ulong id, LeafNode leafNode, int height, IReadOnlyList<byte> keyHash, IEnumerable<byte> value
        )
        {
            var firstFragment = HashFragment(leafNode.KeyHash, height);
            var secondFragment = HashFragment(keyHash, height);
            if (firstFragment != secondFragment)
            {
                var secondSon = NewLeafNode(keyHash, value);
                var secondSonHash = GetNodeById(secondSon)?.Hash;
                if (secondSonHash is null) throw new InvalidOperationException();
                var newId = _versionFactory.NewVersion() ;
                _nodeCache[newId] = InternalNode.WithChildren(
                        new[] {id, secondSon},
                        new[] {firstFragment, secondFragment},
                        new[] {leafNode.Hash, secondSonHash}
                );
                return newId;
            }
            else
            {
                var son = SplitLeafNode(id, leafNode, height + 1, keyHash, value);
                var sonHash = GetNodeById(son)?.Hash;
                if (sonHash is null) throw new InvalidOperationException();
                var newId = _versionFactory.NewVersion();
                _nodeCache[newId] = InternalNode.WithChildren(new[] {son}, new[] {firstFragment}, new[] {sonHash});
                return newId;
            }
        }

        private ulong AddInternal(ulong root, int height, IReadOnlyList<byte> keyHash, byte[] value, bool check)
        {
            if (root == 0)
                return NewLeafNode(keyHash, value);

            var rootNode = GetNodeById(root);
            if (rootNode is null) throw new InvalidOperationException();
            switch (rootNode)
            {
                case InternalNode internalNode:
                    var h = HashFragment(keyHash, height);
                    var to = internalNode.GetChildByHash(h);
                    var updatedTo = AddInternal(to, height + 1, keyHash, value, check);
                    return ModifyInternalNode(root, internalNode, h, updatedTo,
                        GetNodeById(updatedTo)?.Hash ?? throw new InvalidOperationException()
                    );
                case LeafNode leafNode:
                    if (!leafNode.KeyHash.SequenceEqual(keyHash))
                        return SplitLeafNode(root, leafNode, height, keyHash, value);
                    if (check)
                        throw new ArgumentException("Specified keyHash is already present or hash collision occured");
                    return UpdateLeafNode(root, leafNode, value);
            }

            throw new InvalidOperationException($"Unknown node type {root.GetType()}");
        }

        private ulong DeleteInternal(ulong root, int height, byte[] keyHash, out byte[]? value, bool check)
        {
            if (root == 0)
            {
                if (check) throw new KeyNotFoundException(nameof(keyHash));
                value = null;
                return root;
            }

            var rootNode = GetNodeById(root);
            switch (rootNode)
            {
                case InternalNode internalNode:
                    var h = HashFragment(keyHash, height);
                    var to = internalNode.GetChildByHash(h);
                    var updatedTo = DeleteInternal(to, height + 1, keyHash, out value, check);
                    return ModifyInternalNode(root, internalNode, h, updatedTo, GetNodeById(updatedTo)?.Hash);
                case LeafNode leafNode:
                    if (!leafNode.KeyHash.SequenceEqual(keyHash))
                    {
                        if (check) throw new KeyNotFoundException(nameof(keyHash));
                        value = null;
                        return root;
                    }

                    value = leafNode.Value.ToArray();
                    return 0;
            }

            throw new InvalidOperationException($"Unknown node type {root.GetType()}");
        }

        private ulong UpdateInternal(ulong root, int height, byte[] keyHash, byte[] value)
        {
            if (root == 0) throw new KeyNotFoundException(nameof(keyHash));
            var rootNode = GetNodeById(root);
            switch (rootNode)
            {
                case InternalNode internalNode:
                    var h = HashFragment(keyHash, height);
                    var to = internalNode.GetChildByHash(h);
                    var updatedTo = UpdateInternal(to, height + 1, keyHash, value);
                    return ModifyInternalNode(root, internalNode, h, updatedTo,
                        GetNodeById(updatedTo)?.Hash ?? throw new InvalidOperationException());
                case LeafNode leafNode:
                    if (!leafNode.KeyHash.SequenceEqual(keyHash))
                        throw new KeyNotFoundException(nameof(keyHash));
                    return UpdateLeafNode(root, leafNode, value);
            }

            throw new InvalidOperationException($"Unknown node type {root.GetType()}");
        }

        private byte[]? FindInternal(ulong root, IReadOnlyList<byte> keyHash)
        {
            for (var height = 0;; ++height)
            {
                if (root == 0) return null;
                var rootNode = GetNodeById(root);
                switch (rootNode)
                {
                    case InternalNode internalNode:
                        var h = HashFragment(keyHash, height);
                        root = internalNode.GetChildByHash(h);
                        break;
                    case LeafNode leafNode:
                        if (leafNode.KeyHash.SequenceEqual(keyHash)) return leafNode.Value.ToArray();
                        return null;
                    default:
                        throw new InvalidOperationException($"Unknown node type {root.GetType()}");
                }
            }
        }
    }
}