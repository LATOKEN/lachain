using System;
using System.Linq;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;

namespace Lachain.Storage.Trie
{
    internal static class NodeSerializer
    {
        public static byte[] ToBytes(IHashTrieNode node)
        {
            switch (node)
            {
                case InternalNode internalNode:
                    return new byte[] {0}
                        .Concat(internalNode.ChildrenMask.ToBytes())
                        .Concat(internalNode.Hash)
                        .Concat(internalNode.Children.Select(x => x.ToBytes()).Flatten())
                        .ToArray();
                case LeafNode leafNode:
                    return new byte[] {1}.Concat(leafNode.KeyHash).Concat(leafNode.Value).ToArray();
            }

            throw new InvalidOperationException($"Type {node.GetType()} is not supported");
        }

        public static IHashTrieNode FromBytes(ReadOnlyMemory<byte> bytes)
        {
            var type = bytes.Span[0];
            switch (type)
            {
                case 0:
                    var mask = bytes.Slice(1, 4).Span.ToUInt32();
                    var len = (int) BitsUtils.Popcount(mask);
                    var hash = bytes.Slice(1 + 4, 32).ToArray();
                    return new InternalNode(
                        mask,
                        Enumerable.Range(0, len)
                            .Select(i => bytes.Slice(1 + 4 + 32 + i * 8, 8).Span.ToUInt64()),
                        hash
                    );
                case 1:
                    return new LeafNode(bytes.Slice(1, 32).ToArray(), bytes.Slice(1 + 32).ToArray());
                default:
                    throw new InvalidOperationException($"Type id {type} is not supported");
            }
        }
    }
}