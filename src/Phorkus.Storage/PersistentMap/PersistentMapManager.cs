using System;
using System.Collections.Generic;

namespace Phorkus.Storage.PersistentMap
{
    [Obsolete("This no longer inherits IMapManager and is obsolete")]
    public class PersistentMapManager
    {
        private readonly PersistentMapStorageContext _storageContext;
        private readonly Random _random;

        private readonly IDictionary<ulong, PersistentMapNode> _nodeCache =
            new Dictionary<ulong, PersistentMapNode>();

        private readonly ISet<ulong> _persistedNodes = new HashSet<ulong>();
        private readonly VersionFactory _versionFactory;

        public PersistentMapManager(PersistentMapStorageContext storageContext, VersionFactory versionFactory)
        {
            _storageContext = storageContext;
            _versionFactory = versionFactory;
            _random = new Random();
        }

        private PersistentMapNode GetNodeById(ulong id)
        {
            return _nodeCache.TryGetValue(id, out var node) ? node : _storageContext.GetNode(id);
        }

        private uint GetSizeById(ulong id)
        {
            return id == 0 ? 0u : GetNodeById(id).Size;
        }

        private ulong NewNode(ulong left, ulong right, byte[] key, byte[] value)
        {
            var newId = _versionFactory.NewVersion();
            _nodeCache[newId] = new PersistentMapNode(left, right, key, value, GetSizeById(left) + GetSizeById(right) + 1);
            return newId;
        }

        public void Checkpoint(ulong root) // TODO: we can invalidate caches less aggressively
        {
            uint x = EnsurePersisted(root);
            Console.WriteLine($"{x} nodes persisted");
            Console.WriteLine($"{_nodeCache.Count} nodes in cache");
            //ClearCaches();
        }

        private uint EnsurePersisted(ulong root)
        {
            if (!_nodeCache.TryGetValue(root, out var node)) return 0;
            if (_persistedNodes.Contains(root)) return 0;
            uint result = 0;
            result += EnsurePersisted(node.LeftSon);
            result += EnsurePersisted(node.RightSon);
            _storageContext.WriteNode(root, node);
            _persistedNodes.Add(root);
            result += 1;
            return result;
        }

        public void ClearCaches()
        {
            _nodeCache.Clear();
            _persistedNodes.Clear();
        }

        private int CompareKeys(byte[] first, byte[] second)
        {
            int l = Math.Min(first.Length, second.Length);
            for (int i = 0; i < l; ++i)
            {
                if (first[i] != second[i]) return first[i] - second[i];
            }

            return first.Length - second.Length;
        }

        private ulong Merge(ulong left, ulong right)
        {
            if (left == 0) return right;
            if (right == 0) return left;

            var leftNode = GetNodeById(left);
            var rightNode = GetNodeById(right);
            if (CompareKeys(leftNode.Key, rightNode.Key) > 0)
                throw new ArgumentOutOfRangeException(nameof(left));
            if (_random.Next() % (leftNode.Size + rightNode.Size) < leftNode.Size)
            {
                var newRight = Merge(leftNode.RightSon, right);
                return NewNode(leftNode.LeftSon, newRight, leftNode.Key, leftNode.Value);
            }
            else
            {
                var newLeft = Merge(left, rightNode.LeftSon);
                return NewNode(newLeft, rightNode.RightSon, rightNode.Key, rightNode.Value);
            }
        }

        private void Split(ulong root, byte[] key, out ulong left, out ulong right)
        {
            if (root == 0)
            {
                left = 0;
                right = 0;
                return;
            }

            var rootNode = GetNodeById(root);
            if (CompareKeys(rootNode.Key, key) < 0)
            {
                Split(rootNode.RightSon, key, out var newRight, out right);
                left = NewNode(rootNode.LeftSon, newRight, rootNode.Key, rootNode.Value);
            }
            else
            {
                Split(rootNode.LeftSon, key, out left, out var newLeft);
                right = NewNode(newLeft, rootNode.RightSon, rootNode.Key, rootNode.Value);
            }
        }

        public ulong Add(ulong root, byte[] key, byte[] value)
        {
            if (root == 0)
            {
                return NewNode(0, 0, key, value);
            }

            var rootNode = GetNodeById(root);
            var c = CompareKeys(rootNode.Key, key);
            if (c == 0) throw new ArgumentOutOfRangeException(nameof(key));
            if (_random.Next() % (rootNode.Size + 1) == 0)
            {
                Split(root, key, out var newLeft, out var newRight);
                return NewNode(newLeft, newRight, key, value);
            }

            if (c < 0)
            {
                var newRight = Add(rootNode.RightSon, key, value);
                return NewNode(rootNode.LeftSon, newRight, rootNode.Key, rootNode.Value);
            }
            else
            {
                var newLeft = Add(rootNode.LeftSon, key, value);
                return NewNode(newLeft, rootNode.RightSon, rootNode.Key, rootNode.Value);
            }
        }

        public ulong AddOrUpdate(ulong root, byte[] key, byte[] value)
        {
            if (root == 0)
            {
                return NewNode(0, 0, key, value);
            }

            var rootNode = GetNodeById(root);
            var c = CompareKeys(rootNode.Key, key);
            if (c == 0)
            {
                return NewNode(rootNode.LeftSon, rootNode.RightSon, key, value);
            }
            if (_random.Next() % (rootNode.Size + 1) == 0)
            {
                Split(root, key, out var newLeft, out var newRight);
                return NewNode(newLeft, newRight, key, value);
            }

            if (c < 0)
            {
                var newRight = AddOrUpdate(rootNode.RightSon, key, value);
                return NewNode(rootNode.LeftSon, newRight, rootNode.Key, rootNode.Value);
            }
            else
            {
                var newLeft = AddOrUpdate(rootNode.LeftSon, key, value);
                return NewNode(newLeft, rootNode.RightSon, rootNode.Key, rootNode.Value);
            }
        }
        
        public ulong Update(ulong root, byte[] key, byte[] value)
        {
            if (root == 0)
            {
                throw new KeyNotFoundException(nameof(key));
            }

            var rootNode = GetNodeById(root);
            var c = CompareKeys(rootNode.Key, key);
            if (c == 0)
            {
                return NewNode(rootNode.LeftSon, rootNode.RightSon, key, value);
            }
            
            if (c < 0)
            {
                var newRight = Update(rootNode.RightSon, key, value);
                return NewNode(rootNode.LeftSon, newRight, rootNode.Key, rootNode.Value);
            }
            else
            {
                var newLeft = Update(rootNode.LeftSon, key, value);
                return NewNode(newLeft, rootNode.RightSon, rootNode.Key, rootNode.Value);
            }
        }
        
        public ulong Delete(ulong root, byte[] key, out byte[] value)
        {
            if (root == 0)
            {
                throw new KeyNotFoundException(nameof(key));
            }

            var rootNode = GetNodeById(root);
            var c = CompareKeys(key, rootNode.Key);
            if (c == 0)
            {
                value = rootNode.Value;
                return Merge(rootNode.LeftSon, rootNode.RightSon);
            }

            if (c < 0)
            {
                var newLeft = Delete(rootNode.LeftSon, key, out value);
                return newLeft.Equals(rootNode.LeftSon)
                    ? root
                    : NewNode(newLeft, rootNode.RightSon, rootNode.Key, rootNode.Value);
            }
            else
            {
                var newRight = Delete(rootNode.RightSon, key, out value);
                return newRight.Equals(rootNode.RightSon)
                    ? root
                    : NewNode(rootNode.LeftSon, newRight, rootNode.Key, rootNode.Value);
            }
        }

        public ulong TryDelete(ulong root, byte[] key, out byte[] value)
        {
            if (root == 0)
            {
                value = null;
                return root;
            }

            var rootNode = GetNodeById(root);
            var c = CompareKeys(key, rootNode.Key);
            if (c == 0)
            {
                value = rootNode.Value;
                return Merge(rootNode.LeftSon, rootNode.RightSon);
            }

            if (c < 0)
            {
                var newLeft = TryDelete(rootNode.LeftSon, key, out value);
                return newLeft.Equals(rootNode.LeftSon)
                    ? root
                    : NewNode(newLeft, rootNode.RightSon, rootNode.Key, rootNode.Value);
            }
            else
            {
                var newRight = TryDelete(rootNode.RightSon, key, out value);
                return newRight.Equals(rootNode.RightSon)
                    ? root
                    : NewNode(rootNode.LeftSon, newRight, rootNode.Key, rootNode.Value);
            }
        }

        public byte[] Find(ulong root, byte[] key)
        {
            while (true)
            {
                if (root == 0) return null;
                var rootNode = GetNodeById(root);
                var c = CompareKeys(key, rootNode.Key);
                if (c == 0)
                {
                    return rootNode.Value;
                }

                root = c < 0 ? rootNode.LeftSon : rootNode.RightSon;
            }
        }
    }
}