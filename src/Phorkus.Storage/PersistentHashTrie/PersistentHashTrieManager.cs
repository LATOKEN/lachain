using System;
using System.Collections.Generic;
using System.Linq;

namespace Phorkus.Storage.PersistentHashTrie
{
    public class PersistentHashTrieManager : IMapManager
    {
        private readonly PersistentHashTrieStorageContext _storageContext;
        private readonly Random _random;

        private readonly IDictionary<ulong, IHashTrieNode> _nodeCache = new Dictionary<ulong, IHashTrieNode>();

        private readonly ISet<ulong> _persistedNodes = new HashSet<ulong>();
        private readonly VersionFactory _versionFactory;

        public PersistentHashTrieManager(PersistentHashTrieStorageContext storageContext, VersionFactory versionFactory)
        {
            _storageContext = storageContext;
            _versionFactory = versionFactory;
            _random = new Random();
        }

        private void EnsurePersisted(ulong root)
        {
            if (root == 0) return;
            if (!_nodeCache.TryGetValue(root, out var node)) return;
            if (_persistedNodes.Contains(root)) return;
            foreach (var child in node.Children) EnsurePersisted(child);
            _storageContext.WriteNode(root, node);
            _persistedNodes.Add(root);
        }


        public void Checkpoint(ulong root)
        {
            EnsurePersisted(root);
            ClearCaches();
        }

        public void ClearCaches()
        {
            _nodeCache.Clear();
            _persistedNodes.Clear();
        }

        private static uint Hash(IEnumerable<byte> key)
        {
            unchecked
            {
                return key.Aggregate(0u, (current, b) => current * 1131861u + b);
            }
        }

        public ulong Add(ulong root, byte[] key, byte[] value)
        {
            var hash = Hash(key);
            return AddInternal(root, hash, 0, key, value);
        }

        public ulong AddOrUpdate(ulong root, byte[] key, byte[] value)
        {
            var hash = Hash(key);
            return AddOrUpdateInternal(root, hash, 0, key, value);
        }

        public ulong Update(ulong root, byte[] key, byte[] value)
        {
            var hash = Hash(key);
            return UpdateInternal(root, hash, 0, key, value);
        }

        public ulong Delete(ulong root, byte[] key, out byte[] value)
        {
            var hash = Hash(key);
            return DeleteInternal(root, hash, 0, key, out value);
        }

        public ulong TryDelete(ulong root, byte[] key, out byte[] value)
        {
            var hash = Hash(key);
            return TryDeleteInternal(root, hash, 0, key, out value);
        }

        public byte[] Find(ulong root, byte[] key)
        {
            var hash = Hash(key);
            return FindInternal(root, hash, key);
        }

        public IEnumerable<byte[]> GetKeys(ulong root)
        {
            return Traverse(root, 0).Select(pair => pair.Key);
        }

        public IEnumerable<byte[]> GetValues(ulong root)
        {
            return Traverse(root, 0).Select(pair => pair.Value);
        }

        public IEnumerable<KeyValuePair<byte[], byte[]>> GetEntries(ulong root)
        {
            return Traverse(root, 0);
        }

        private IEnumerable<KeyValuePair<byte[], byte[]>> Traverse(ulong root, int height)
        {
            if (height == 7)
            {
                if (root == 0) yield break;
                foreach (var pair in (GetNodeById(root) as LeafNode).Pairs)
                    yield return pair;
                yield break;
            }

            if (root == 0) yield break;
            foreach (var son in GetNodeById(root).Children)
            foreach (var entry in Traverse(son, height + 1))
                yield return entry;
        }

        private IHashTrieNode GetNodeById(ulong id)
        {
            return _nodeCache.TryGetValue(id, out var node) ? node : _storageContext.GetNode(id);
        }

        private ulong NewLeafNode(byte[] key, byte[] value)
        {
            var newId = _versionFactory.NewVersion();
            _nodeCache[newId] = new LeafNode(key, value);
            return newId;
        }

        private ulong ModifyInternalNode(InternalNode node, byte h, ulong value)
        {
            var newId = _versionFactory.NewVersion();
            _nodeCache[newId] = InternalNode.ModifyChildren(node, h, value);
            return newId;
        }

        private ulong InsertInLeafNode(LeafNode node, byte[] key, byte[] value)
        {
            var newId = _versionFactory.NewVersion();
            _nodeCache[newId] = LeafNode.Insert(node, key, value);
            return newId;
        }

        private ulong TryDeleteInLeafNode(LeafNode node, byte[] key, out byte[] value)
        {
            var newId = _versionFactory.NewVersion();
            _nodeCache[newId] = LeafNode.Delete(node, key, out value);
            return newId;
        }

        private ulong InsertOrUpdateInLeafNode(LeafNode node, byte[] key, byte[] value)
        {
            var newId = _versionFactory.NewVersion();
            _nodeCache[newId] = LeafNode.InsertOrUpdate(node, key, value);
            return newId;
        }

        private ulong UpdateInLeafNode(LeafNode node, byte[] key, byte[] value)
        {
            var newId = _versionFactory.NewVersion();
            _nodeCache[newId] = LeafNode.Update(node, key, value);
            return newId;
        }

        private ulong DeleteInLeafNode(LeafNode node, byte[] key, out byte[] value)
        {
            var newId = _versionFactory.NewVersion();
            _nodeCache[newId] = LeafNode.Delete(node, key, out value, true);
            return newId;
        }

        private byte HashFragment(uint hash, int n)
        {
            return (byte) ((hash >> (5 * n)) & 0x1F);
        }

        private ulong AddInternal(ulong root, uint hash, int height, byte[] key, byte[] value)
        {
            if (height == 7)
            {
                if (root == 0) return NewLeafNode(key, value);
                return InsertInLeafNode(GetNodeById(root) as LeafNode, key, value);
            }

            var h = HashFragment(hash, height);
            if (root == 0)
            {
                var son = AddInternal(0, hash, height + 1, key, value);
                return ModifyInternalNode(null, h, son);
            }

            var rootNode = GetNodeById(root);
            var to = rootNode.GetChildByHash(h);
            return ModifyInternalNode(rootNode as InternalNode, h, AddInternal(to, hash, height + 1, key, value));
        }

        private ulong AddOrUpdateInternal(ulong root, uint hash, int height, byte[] key, byte[] value)
        {
            if (height == 7)
            {
                if (root == 0) return NewLeafNode(key, value);
                return InsertOrUpdateInLeafNode(GetNodeById(root) as LeafNode, key, value);
            }

            var h = HashFragment(hash, height);
            if (root == 0)
            {
                var son = AddOrUpdateInternal(0, hash, height + 1, key, value);
                return ModifyInternalNode(null, h, son);
            }

            var rootNode = GetNodeById(root);
            var to = rootNode.GetChildByHash(h);
            return ModifyInternalNode(rootNode as InternalNode, h,
                AddOrUpdateInternal(to, hash, height + 1, key, value));
        }

        private ulong DeleteInternal(ulong root, uint hash, int height, byte[] key, out byte[] value)
        {
            if (root == 0) throw new KeyNotFoundException(nameof(key));
            var rootNode = GetNodeById(root);
            if (height == 7) return DeleteInLeafNode(rootNode as LeafNode, key, out value);
            var h = HashFragment(hash, height);
            var to = rootNode.GetChildByHash(h);
            return ModifyInternalNode(
                rootNode as InternalNode, h, DeleteInternal(to, hash, height + 1, key, out value)
            );
        }

        private ulong UpdateInternal(ulong root, uint hash, int height, byte[] key, byte[] value)
        {
            if (root == 0) throw new KeyNotFoundException(nameof(key));
            var rootNode = GetNodeById(root);
            if (height == 7) return UpdateInLeafNode(rootNode as LeafNode, key, value);
            var h = HashFragment(hash, height);
            var to = rootNode.GetChildByHash(h);
            return ModifyInternalNode(
                rootNode as InternalNode, h, UpdateInternal(to, hash, height + 1, key, value)
            );
        }

        private ulong TryDeleteInternal(ulong root, uint hash, int height, byte[] key, out byte[] value)
        {
            if (root == 0)
            {
                value = null;
                return 0;
            }

            var rootNode = GetNodeById(root);
            if (height == 7) return TryDeleteInLeafNode(rootNode as LeafNode, key, out value);
            var h = HashFragment(hash, height);
            var to = rootNode.GetChildByHash(h);
            return ModifyInternalNode(
                rootNode as InternalNode, h, TryDeleteInternal(to, hash, height + 1, key, out value)
            );
        }

        private byte[] FindInternal(ulong root, uint hash, byte[] key)
        {
            for (var height = 0; height < 7; ++height)
            {
                if (root == 0) return null;
                var rootNode = GetNodeById(root);
                root = rootNode.GetChildByHash(HashFragment(hash, height));
            }

            return (GetNodeById(root) as LeafNode)?.Find(key);
        }
    }
}