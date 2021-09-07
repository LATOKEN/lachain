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
                    byte[] nodeByte = new byte[] {0}
                        .Concat(internalNode.ChildrenMask.ToBytes())
                        .Concat(internalNode.Hash)
                        .ToArray();
                    var _children = internalNode.Children.ToArray();
                    uint sz = 0;
                    for (int i = 0; i < _children.Length; i++)
                    {
                        ulong childId = _children[i];
                        sz++;
                        while (childId > 0)
                        {
                            childId >>= 8;
                            sz++;
                        }
                    }
                    byte[] childByte = new byte[sz];
                    uint idx = 0;
                    for (int i = 0; i < _children.Length; i++)
                    {
                        ulong childId = _children[i];
                        byte curSize = 0;
                        while (childId > 0)
                        {
                            curSize++;
                            childByte[idx + curSize] = (byte)(childId & 255);
                            childId >>= 8;
                        }
                        childByte[idx] = curSize;
                        idx += (uint)(curSize + 1);
                    }
                    return nodeByte.Concat(childByte).ToArray();
                case LeafNode leafNode:
                    return new byte[] {1}.Concat(leafNode.KeyHash).Concat(leafNode.Value).ToArray();
            }

            throw new InvalidOperationException($"Type {node.GetType()} is not supported");
        }

        public static IHashTrieNode FromBytes(ReadOnlyMemory<byte> bytes)
        {
            if (bytes.Length < 1)
            {
                throw new ArgumentException("Empty bytes to deserialize");
            }
            var type = bytes.Span[0];
            switch (type)
            {
                case 0:
                    if (bytes.Length < 1 + 4 + 32)
                    {
                        throw new ArgumentException($"Not enough bytes to deserialize: {bytes.Length}");
                    }
                    var mask = bytes.Slice(1, 4).Span.ToUInt32();
                    var len = (int) BitsUtils.Popcount(mask);
                    var hash = bytes.Slice(1 + 4, 32).ToArray();

                    ulong[] _children = new ulong[len];
                    var pos = 32 + 5;

                    for (var i = 0; i < len; i++)
                    {
                        if (pos >= bytes.Length) throw new ArgumentException("does not hold all the childen");
                        byte sz = bytes.Span[pos];
                        
                        if(pos + sz >= bytes.Length) throw new ArgumentException("not enough bytes to deserialize the children id"); 
                        
                        ulong m = 1;
                        ulong res = 0;
                        for (int j = pos + 1; j <= pos + sz; j++)
                        {
                            res = res + m * bytes.Span[j];
                            m = m * 256;
                        }
                        _children[i] = res;
                        pos += sz + 1;
                    }
                    return new InternalNode(mask, _children, hash);
                case 1:
                    if (bytes.Length < 1 + 32)
                    {
                        throw new ArgumentException($"Not enough bytes to deserialize: {bytes.Length}");
                    }
                    return new LeafNode(bytes.Slice(1, 32).ToArray(), bytes.Slice(1 + 32).ToArray());
                default:
                    throw new InvalidOperationException($"Type id {type} is not supported");
            }
        }
    }
}
