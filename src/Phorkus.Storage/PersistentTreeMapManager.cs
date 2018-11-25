using System;

namespace Phorkus.Storage
{
    public class PersistentTreeMapManager<TIDentifier, TKey, TValue>
        where TIDentifier : IEquatable<TIDentifier>
        where TKey : IComparable<TKey>
    {
        private readonly IPersistentMapStorageContext<TIDentifier, TKey, TValue> _context;
        private readonly Random _random;

        public PersistentTreeMapManager(IPersistentMapStorageContext<TIDentifier, TKey, TValue> context)
        {
            _context = context;
            _random = new Random();
        }

        public TIDentifier Merge(TIDentifier left, TIDentifier right)
        {
            if (left.Equals(_context.NullIDentifier)) return right;
            if (right.Equals(_context.NullIDentifier)) return left;

            var leftNode = _context.GetNodeById(left);
            var rightNode = _context.GetNodeById(right);
            if (leftNode.Key.CompareTo(rightNode.Key) > 0)
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

        public void Split(TIDentifier root, TKey key, out TIDentifier left, out TIDentifier right)
        {
            if (root.Equals(_context.NullIDentifier))
            {
                left = _context.NullIDentifier;
                right = _context.NullIDentifier;
                return;
            }
            var rootNode = _context.GetNodeById(root);
            if (rootNode.Key.CompareTo(key) < 0)
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

        public TIDentifier Add(TIDentifier root, TKey key, TValue value)
        {
            if (root.Equals(_context.NullIDentifier))
            {
                return _context.NewNode(_context.NullIDentifier, _context.NullIDentifier, key, value);
            }
            var rootNode = _context.GetNodeById(root);
            var c = rootNode.Key.CompareTo(key);
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