using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;

namespace Phorkus.Storage.Treap
{
    public class PersistentTreeMapManager<TKey, TValue, TComparator>
        where TComparator : IComparer<TKey>
        where TKey : IMessage
        where TValue : class, IMessage
    {
        private readonly IPersistentMapStorageContext<TKey, TValue> _context;
        private readonly Random _random;
        private readonly TComparator _comparator;
        private readonly IPersistentTreeMapFactory _factory;

        private readonly IDictionary<ulong, PersistentTreeMapNode<TKey, TValue>> _nodeCache =
            new Dictionary<ulong, PersistentTreeMapNode<TKey, TValue>>();
        private readonly ISet<ulong> _pesistedNodes = new HashSet<ulong>();

        public PersistentTreeMapManager(IPersistentMapStorageContext<TKey, TValue> context, TComparator comparator, IPersistentTreeMapFactory factory)
        {
            _context = context;
            _comparator = comparator;
            _factory = factory;
            _random = new Random();
        }

        public PersistentTreeMapNode<TKey, TValue> GetNodeById(IPersistentTreeMap id)
        {
            if (_nodeCache.TryGetValue(id.Id, out var node)) return node;
            return _context.GetNodeById(id);
        }

        public IPersistentTreeMap NewNode(
            IPersistentTreeMap left, IPersistentTreeMap right, TKey key, TValue value
        )
        {
            var newId = _factory.NewVersionId();
            _nodeCache[newId.Id] = new PersistentTreeMapNode<TKey, TValue>(left, right, key, value);
            return newId;
        }

        public void Checkpoint(IPersistentTreeMap root)
        {
            if (!_nodeCache.TryGetValue(root.Id, out var node)) return;
            if (_pesistedNodes.Contains(root.Id)) return;
            Checkpoint(node.LeftSon);
            Checkpoint(node.RightSon);
            _context.PersistNode(root, node);
            _pesistedNodes.Add(root.Id);
        }

        public void ClearCaches()
        {
            _nodeCache.Clear();
            _pesistedNodes.Clear();
        }

        public IPersistentTreeMap Merge(IPersistentTreeMap left, IPersistentTreeMap right)
        {
            if (left.Equals(_factory.NullIdentifier)) return right;
            if (right.Equals(_factory.NullIdentifier)) return left;

            var leftNode = GetNodeById(left);
            var rightNode = GetNodeById(right);
            if (_comparator.Compare(leftNode.Key, rightNode.Key) > 0)
                throw new ArgumentOutOfRangeException(nameof(left));
            if ((_random.Next() & 1) == 0)
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

        public void Split(IPersistentTreeMap root, TKey key, out IPersistentTreeMap left, out IPersistentTreeMap right)
        {
            if (root.Equals(_factory.NullIdentifier))
            {
                left = _factory.NullIdentifier;
                right = _factory.NullIdentifier;
                return;
            }

            var rootNode = GetNodeById(root);
            if (_comparator.Compare(rootNode.Key, key) < 0)
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

        public IPersistentTreeMap Add(IPersistentTreeMap root, TKey key, TValue value)
        {
            if (root.Equals(_factory.NullIdentifier))
            {
                return NewNode(_factory.NullIdentifier, _factory.NullIdentifier, key, value);
            }

            var rootNode = GetNodeById(root);
            var c = _comparator.Compare(rootNode.Key, key);
            if (c == 0) throw new ArgumentOutOfRangeException(nameof(key));
            if ((_random.Next() & 1) == 0)
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

        public IPersistentTreeMap TryDelete(IPersistentTreeMap root, TKey key, out TValue value)
        {
            if (root.Equals(_factory.NullIdentifier))
            {
                value = null;
                return root;
            }

            var rootNode = GetNodeById(root);
            var c = _comparator.Compare(key, rootNode.Key);
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

        public IEnumerable<TKey> GetKeys(IPersistentTreeMap root)
        {
            if (root.Equals(_factory.NullIdentifier))
            {
                return new List<TKey>();
            }

            var rootNode = GetNodeById(root);
            return GetKeys(rootNode.LeftSon).Concat(new[] {rootNode.Key}.Concat(GetKeys(rootNode.RightSon)));
        }
    }
}