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
                    byte[] nodeByte = new byte[] { 0 }
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
                    return new byte[] { 1 }.Concat(leafNode.KeyHash).Concat(leafNode.Value).ToArray();
            }

            throw new InvalidOperationException($"Type {node.GetType()} is not supported");
        }

        public static IHashTrieNode FromBytes(byte[] bytes)
        {
            if (bytes.Length < 1)
            {
                throw new ArgumentException("Empty bytes to deserialize");
            }
            var type = bytes[0];
            switch (type)
            {
                case 0:
                    if (bytes.Length < 1 + 4)
                    {
                        throw new ArgumentException($"Not enough bytes to deserialize: {bytes.Length}");
                    }
                    UInt32 mask = 0;
                    UInt32 mul = 1;
                    for (var i = 1; i <= 4; i++)
                    {
                        mask += mul * bytes[i];
                        if (i < 4) mul = mul * 256;
                    }
                    var len = (int)BitsUtils.Popcount(mask);
                    byte[] hash = new byte[32];
                    for (var i = 0; i < 32; i++)
                    {
                        hash[i] = bytes[i + 5];
                    }
                    ulong[] _children = new ulong[len];
                    var pos = 32 + 5;
                    for (var i = 0; i < len; i++)
                    {
                        byte sz = bytes[pos];
                        ulong m = 1;
                        ulong res = 0;
                        for (int j = pos + 1; j <= pos + sz; j++)
                        {
                            res = res + m * bytes[j];
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
                    byte[] key = new byte[32];
                    byte[] value = new byte[32];
                    for (var i = 1; i <= 32; i++)
                    {
                        key[i - 1] = bytes[i];
                        value[i - 1] = bytes[i + 32];
                    }
                    return new LeafNode(key, value);
                default:
                    throw new InvalidOperationException($"Type id {type} is not supported");
            }
        }
    }
}