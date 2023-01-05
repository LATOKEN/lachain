using System.Collections.Generic;
using System.Linq;
using Lachain.Crypto;
using Lachain.Utility.Serialization;

namespace Lachain.Storage.Trie
{
    public class LeafNode : IHashTrieNode
    {
        public readonly byte[] KeyHash;
        public readonly byte[] Value;

        public LeafNode(IEnumerable<byte> keyHash, IEnumerable<byte> value)
        {
            KeyHash = keyHash.ToArray();
            Value = value.ToArray();
            Hash = KeyHash.Length.ToBytes().Concat(KeyHash).Concat(Value).KeccakBytes();
        }

        public NodeType Type { get; } = NodeType.Leaf;

        public byte[] Hash { get; }

        public ulong GetChildByHash(byte h)
        {
            return 0u;
        }

        public IEnumerable<ulong> Children { get; } = Enumerable.Empty<ulong>();

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((IHashTrieNode) obj);
        }

        public bool Equals(IHashTrieNode? other)
        {
            return !(other is null) && Hash.SequenceEqual(other.Hash);
        }

        public override int GetHashCode()
        {
            return Hash.Length;
        }
    }
}