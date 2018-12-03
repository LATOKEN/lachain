using System;
using System.Collections.Generic;
using System.Linq;
using Org.BouncyCastle.Crypto.Modes;

namespace Phorkus.Hestia.PersistentHashTrie
{
    public class LeafNode : IHashTrieNode
    {
        public List<KeyValuePair<byte[], byte[]>> Pairs { get; }

        public LeafNode(byte[] key, byte[] value)
        {
            Pairs = new[] {new KeyValuePair<byte[], byte[]>(key, value)}.ToList();
        }

        public LeafNode(IEnumerable<KeyValuePair<byte[], byte[]>> pairs)
        {
            Pairs = pairs.ToList();
        }

        public NodeType Type { get; } = NodeType.Leaf;

        public ulong GetChildByHash(byte h)
        {
            return 0u;
        }

        public IEnumerable<ulong> Children { get; } = Enumerable.Empty<ulong>();

        public static LeafNode Insert(LeafNode node, byte[] key, byte[] value, bool check = false)
        {
            if (check && node.Pairs.Any(pair => pair.Key.SequenceEqual(key)))
                throw new InvalidOperationException("Key already exists");
            return new LeafNode(node.Pairs.Append(new KeyValuePair<byte[], byte[]>(key, value)));
        }

        public static LeafNode InsertOrUpdate(LeafNode node, byte[] key, byte[] value)
        {
            var found = false;
            var copy = new LeafNode(node.Pairs);
            for (var i = 0; i < copy.Pairs.Count; ++i)
            {
                if (!copy.Pairs[i].Key.SequenceEqual(key)) continue;
                found = true;
                copy.Pairs[i] = new KeyValuePair<byte[], byte[]>(key, value);
                break;
            }

            if (!found)
            {
                copy.Pairs.Add(new KeyValuePair<byte[], byte[]>(key, value));
            }

            return copy;
        }

        public static LeafNode Update(LeafNode node, byte[] key, byte[] value, bool check = false)
        {
            if (check && !node.Pairs.Any(pair => pair.Key.SequenceEqual(key)))
                throw new InvalidOperationException("Key not found");
            return new LeafNode(
                node.Pairs.Select(pair =>
                    new KeyValuePair<byte[], byte[]>(pair.Key, pair.Key.SequenceEqual(key) ? value : pair.Value)
                )
            );
        }

        public static LeafNode Delete(LeafNode node, byte[] key, out byte[] value, bool check = false)
        {
            var copy = new LeafNode(node.Pairs);
            for (var i = 0; i < copy.Pairs.Count; ++i)
            {
                if (!copy.Pairs[i].Key.SequenceEqual(key)) continue;
                value = copy.Pairs[i].Value;
                copy.Pairs.RemoveAt(i);
                return copy;
            }

            if (check) throw new InvalidOperationException("Key not found");
            value = null;
            return copy;
        }

        public byte[] Find(byte[] key)
        {
            return Pairs.Find(pair => key.SequenceEqual(pair.Key)).Value;
        }
    }
}