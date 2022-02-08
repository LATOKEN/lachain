using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Crypto;
using Lachain.Utility.Utils;

namespace Lachain.Storage.Trie
{
    public class InternalNode : IHashTrieNode
    {
        public uint ChildrenMask { get; private set; }

        private ulong[] _children;

        public NodeType Type { get; } = NodeType.Internal;
        public byte[] Hash { get; private set; }

        public ulong GetChildByHash(byte h)
        {
            return (ChildrenMask & (1u << h)) == 0 ? 0ul : _children[BitsUtils.PositionOf(ChildrenMask, h)];
        }

        public IEnumerable<ulong> Children => _children;

        public InternalNode()
        {
            ChildrenMask = 0;
            _children = new ulong[0];
            Hash = new byte[] { };
            UpdateHash(Enumerable.Empty<byte[]>(), Enumerable.Empty<byte>());
        }

        public InternalNode(uint mask, IEnumerable<ulong> children, IEnumerable<byte[]> childrenHashes)
        {
            ChildrenMask = mask;
            _children = children.ToArray();
            Hash = new byte[] { };
            UpdateHash(childrenHashes, GetChildrenLabels(mask));
        }

        public InternalNode(uint mask, IEnumerable<ulong> children, IEnumerable<byte> hash)
        {
            ChildrenMask = mask;
            _children = children.ToArray();
            Hash = hash.ToArray();
        }

        public static IEnumerable<byte> GetChildrenLabels(uint mask)
        {
            return Enumerable.Range(0, 32).Where(i => ((mask >> i) & 1) != 0).Select(i => (byte) i);
        }

        

        private void UpdateHash(IEnumerable<byte[]> childrenHashesList, IEnumerable<byte> childrenLabelsList)
        {
            var childrenHashes = childrenHashesList.ToArray();
            var childrenLabels = childrenLabelsList.ToArray();
            //need to change here
            Hash = new byte[32];
//            for(int i=0; i<32; i++) Hash[i] = (byte)0;
            for(int i=0; i<childrenHashes.Count(); i++)
            {
                ModifyHash(childrenHashes[i], childrenLabels[i]);
            }
/*            Hash = childrenHashes
                .Zip(childrenLabels, (bytes, i) => new[] {i}.Concat(bytes))
                .SelectMany(bytes => bytes)
                .KeccakBytes(); */
        }

        private void ModifyHash(byte[] childrenHash, byte childrenLabel)
        {
            var newHash = new [] {childrenLabel}.Concat(childrenHash).KeccakBytes();
            for(int i=0; i<newHash.Count(); i++) Hash[i] ^= newHash[i];
        }

        public static InternalNode WithChildren(
            IEnumerable<ulong> childrenIds, IEnumerable<byte> childrenLabels, IEnumerable<byte[]> childrenHashes)
        {
            var labels = childrenLabels.ToList();
            var ids = childrenIds.ToList();
            if (labels.Distinct().Count() != labels.Count)
                throw new ArgumentException("Trying to create internal trie node with equal children labels");
            var mask = (uint) labels.Sum(x => 1u << x);
            var hashes = childrenHashes.ToList();
            return new InternalNode(
                mask,
                labels
                    .Zip(ids, (label, id) => new KeyValuePair<ulong, byte>(id, label))
                    .OrderBy(pair => pair.Value)
                    .Select(pair => pair.Key),
                labels
                    .Zip(hashes, (label, hash) => new KeyValuePair<byte[], byte>(hash,label))
                    .OrderBy(pair => pair.Value)
                    .Select(pair => pair.Key)
            );
        }

        public static IHashTrieNode? ModifyChildren(
            InternalNode node, byte h, ulong value, byte[]? valueHash, byte[] childHash
        )
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            var was = node.GetChildByHash(h);
            if (was == value)
            {
                return node;
            }

//            List<byte[]> newHashes;
            var newNode = new InternalNode();
            var pos = (int) BitsUtils.PositionOf(node.ChildrenMask, h);
            newNode.Hash = new byte[node.Hash.Length];
            for(var i=0; i<node.Hash.Length; ++i)
                newNode.Hash[i] = node.Hash[i];

            if (was == 0)
            {
                if (valueHash is null) throw new ArgumentNullException(nameof(valueHash));
                newNode._children = new ulong[node._children.Length + 1];
                for (var i = 0; i <= node._children.Length; ++i)
                    newNode._children[i] = i < pos ? node._children[i] : (i == pos ? value : node._children[i - 1]);
                newNode.ChildrenMask = node.ChildrenMask | (1u << h);
                newNode.ModifyHash(valueHash, h);
//                newHashes = childrenHashes.ToList();
//                newHashes.Insert(pos, valueHash);
//                newNode.UpdateHash(newHashes, GetChildrenLabels(newNode.ChildrenMask));
                return newNode;
            }

            if (value == 0)
            {
                if (node._children.Length == 1) return null;
                newNode._children = new ulong[node._children.Length - 1];
                for (var i = 0; i + 1 < node._children.Length; ++i)
                    newNode._children[i] = i < pos ? node._children[i] : node._children[i + 1];
                newNode.ChildrenMask = node.ChildrenMask ^ (1u << h);
                newNode.ModifyHash(childHash, h);
            //    newHashes = childrenHashes.ToList();
            //    newHashes.RemoveAt(pos);
            //    newNode.UpdateHash(newHashes, GetChildrenLabels(newNode.ChildrenMask));
                return newNode;
            }

            
            

            newNode._children = node._children.ToArray();
            newNode.ChildrenMask = node.ChildrenMask;
            newNode._children[pos] = value;
//            newHashes = childrenHashes.ToList();
            newNode.ModifyHash(valueHash, h);
//            newHashes[pos] = valueHash ?? throw new ArgumentNullException(nameof(valueHash));
//            newNode.UpdateHash(newHashes, GetChildrenLabels(newNode.ChildrenMask));
            return newNode;
        }
    }
}