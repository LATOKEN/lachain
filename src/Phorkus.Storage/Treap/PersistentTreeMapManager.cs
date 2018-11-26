using System;
using System.Collections.Generic;
using Google.Protobuf;

namespace Phorkus.Storage.Treap
{
    public class PersistentTreeMapManager<TKey, TValue, TComparator>
        where TComparator : IComparer<TKey>
        where TKey : IMessage
        where TValue : IMessage
    {
        private readonly IPersistentMapStorageContext<TKey, TValue> _context;
        private readonly Random _random;
        private readonly TComparator _comparator;

        public PersistentTreeMapManager(IPersistentMapStorageContext<TKey, TValue> context, TComparator comparator)
        {
            _context = context;
            _comparator = comparator;
            _random = new Random();
        }

        public IPersistentTreeMap Merge(IPersistentTreeMap left, IPersistentTreeMap right)
        {
            if (left.Equals(_context.NullIDentifier)) return right;
            if (right.Equals(_context.NullIDentifier)) return left;

            var leftNode = _context.GetNodeById(left);
            var rightNode = _context.GetNodeById(right);
            if (_comparator.Compare(leftNode.Key, rightNode.Key) > 0)
                throw new ArgumentOutOfRangeException(nameof(left));
            if ((_random.Next() & 1) == 0)
            {
                var newRight = Merge(leftNode.RightSon, right);
                return _context.NewNode(leftNode.LeftSon, newRight, leftNode.Key, leftNode.Value);
            }
            else
            {
                var newLeft = Merge(left, rightNode.LeftSon);
                return _context.NewNode(newLeft, rightNode.RightSon, rightNode.Key, rightNode.Value);
            }
        }

        public void Split(IPersistentTreeMap root, TKey key, out IPersistentTreeMap left, out IPersistentTreeMap right)
        {
            if (root.Equals(_context.NullIDentifier))
            {
                left = _context.NullIDentifier;
                right = _context.NullIDentifier;
                return;
            }

            var rootNode = _context.GetNodeById(root);
            if (_comparator.Compare(rootNode.Key, key) < 0)
            {
                Split(rootNode.RightSon, key, out var newRight, out right);
                left = _context.NewNode(rootNode.LeftSon, newRight, rootNode.Key, rootNode.Value);
            }
            else
            {
                Split(rootNode.LeftSon, key, out left, out var newLeft);
                right = _context.NewNode(newLeft, rootNode.RightSon, rootNode.Key, rootNode.Value);
            }
        }

        public IPersistentTreeMap Add(IPersistentTreeMap root, TKey key, TValue value)
        {
            if (root.Equals(_context.NullIDentifier))
            {
                return _context.NewNode(_context.NullIDentifier, _context.NullIDentifier, key, value);
            }

            var rootNode = _context.GetNodeById(root);
            var c = _comparator.Compare(rootNode.Key, key);
            if (c == 0) throw new ArgumentOutOfRangeException(nameof(key));
            if ((_random.Next() & 1) == 0)
            {
                Split(root, key, out var newLeft, out var newRight);
                return _context.NewNode(newLeft, newRight, key, value);
            }

            if (c < 0)
            {
                var newRight = Add(rootNode.RightSon, key, value);
                return _context.NewNode(rootNode.LeftSon, newRight, rootNode.Key, rootNode.Value);
            }
            else
            {
                var newLeft = Add(rootNode.LeftSon, key, value);
                return _context.NewNode(newLeft, rootNode.RightSon, rootNode.Key, rootNode.Value);
            }
        }
    }
}