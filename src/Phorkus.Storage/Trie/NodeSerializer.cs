using System;
using System.Collections.Generic;
using System.Linq;
using Phorkus.Utility.Utils;

namespace Phorkus.Storage.Trie
{
    public static class NodeSerializer
    {
        private static IEnumerable<byte> SerializePair(KeyValuePair<byte[], byte[]> pair)
        {
            return BitConverter.GetBytes(pair.Key.Length).Concat(pair.Key)
                .Concat(BitConverter.GetBytes(pair.Value.Length)).Concat(pair.Value);
        }

        public static byte[] ToByteArray(IHashTrieNode node)
        {
            switch (node)
            {
                case InternalNode internalNode:
                    return new byte[] {0}
                        .Concat(BitConverter.GetBytes(internalNode.ChildrenMask))
                        .Concat(internalNode.Children.Select(BitConverter.GetBytes).SelectMany(bytes => bytes))
                        .ToArray();
                case LeafNode leafNode:
                    return new byte[] {1}.Concat(leafNode.Pairs.SelectMany(SerializePair)).ToArray();
            }

            throw new NotImplementedException($"Type {node.GetType()} is not supported");
        }

        public static IHashTrieNode FromBytes(byte[] bytes)
        {
            var type = bytes[0];
            if (type == 0)
            {
                var mask = BitConverter.ToUInt32(bytes, 1);
                var len = (int) BitsUtils.Popcount(mask);
                return new InternalNode(
                    mask,
                    Enumerable.Range(0, len).Select(i => BitConverter.ToUInt64(bytes, 5 + i * 8))
                );
            }

            if (type == 1)
            {
                var offset = 1;
                var pairs = new List<KeyValuePair<byte[], byte[]>>();
                while (offset < bytes.Length)
                {
                    var keyLen = BitConverter.ToInt32(bytes, offset);
                    var valueLen = BitConverter.ToInt32(bytes, offset + 4 + keyLen);
                    var key = new byte[keyLen];
                    var value = new byte[valueLen];
                    Array.Copy(bytes, offset + 4, key, 0, keyLen);
                    Array.Copy(bytes, offset + 8 + keyLen, value, 0, valueLen);
                    pairs.Add(new KeyValuePair<byte[], byte[]>(key, value));
                    offset += 8 + keyLen + valueLen;
                }

                return new LeafNode(pairs);
            }

            throw new NotImplementedException($"Type id {type} is not supported");
        }
    }
}