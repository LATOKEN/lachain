using System;
using System.Linq;
using Lachain.Utility.Utils;

namespace Lachain.Storage.Trie
{
    internal static class NodeSerializer
    {
        public static byte[] ToByteArray(IHashTrieNode node)
        {
            switch (node)
            {
                case InternalNode internalNode:
                    return new byte[] {0}
                        .Concat(BitConverter.GetBytes(internalNode.ChildrenMask))
                        .Concat(internalNode.Hash)
                        .Concat(internalNode.Children.Select(BitConverter.GetBytes).SelectMany(bytes => bytes))
                        .ToArray();
                case LeafNode leafNode:
                    return new byte[] {1}.Concat(leafNode.KeyHash).Concat(leafNode.Value).ToArray();
            }

            throw new InvalidOperationException($"Type {node.GetType()} is not supported");
        }

        public static IHashTrieNode FromBytes(byte[] bytes)
        {
            var type = bytes[0];
            switch (type)
            {
                case 0:
                    var mask = BitConverter.ToUInt32(bytes, 1);
                    var len = (int) BitsUtils.Popcount(mask);
                    var hash = bytes.Skip(1 + 4).Take(32).ToArray();
                    return new InternalNode(
                        mask,
                        Enumerable.Range(0, len).Select(i => BitConverter.ToUInt64(bytes, 1 + 4 + 32 + i * 8)),
                        hash
                    );
                case 1:
                    return new LeafNode(bytes.Skip(1).Take(32), bytes.Skip(1 + 32));
                default:
                    throw new InvalidOperationException($"Type id {type} is not supported");
            }
        }
    }
}