using System.Collections.Generic;
using System.Linq;

namespace Phorkus.Storage.PersistentHashTrie
{
    public class InternalNode : IHashTrieNode
    {
        public uint ChildrenMask { get; private set; }
        private ulong[] _children;
        
        public NodeType Type { get; } = NodeType.Internal;

        public ulong GetChildByHash(byte h)
        {
            return (ChildrenMask & (1u << h)) == 0 ? 0ul : _children[BitUtils.PositionOf(ChildrenMask, h)];
        }
        
        public IEnumerable<ulong> Children => _children;

        private InternalNode() {}
        internal InternalNode(uint mask, IEnumerable<ulong> children)
        {
            ChildrenMask = mask;
            _children = children.ToArray();
        }

        public static InternalNode ModifyChildren(InternalNode node, byte h, ulong value)
        {
            if (node == null)
            {
                node = new InternalNode();
                if (value == 0)
                {
                    node._children = new ulong[0];
                    node.ChildrenMask = 0;
                }
                else
                {
                    node._children = new[]{value};
                    node.ChildrenMask = 1u << h;
                }

                return node;
            }
            var was = node.GetChildByHash(h);
            if (was == value)
            {
                var copy = new InternalNode
                {
                    _children = node._children.ToArray(),
                    ChildrenMask = node.ChildrenMask
                };
                return copy;
            }
            var newNode = new InternalNode();
            var pos = BitUtils.PositionOf(node.ChildrenMask, h);
            if (was == 0)
            {
                newNode._children = new ulong[node._children.Length + 1];
                for (var i = 0; i <= node._children.Length; ++i)
                    newNode._children[i] = i < pos ? node._children[i] : (i == pos ? value : node._children[i - 1]);
                newNode.ChildrenMask = node.ChildrenMask | (1u << h);
                return newNode;
            }

            if (value == 0)
            {
                newNode._children = new ulong[node._children.Length - 1];
                for (var i = 0; i + 1 < node._children.Length; ++i)
                    newNode._children[i] = i < pos ? node._children[i] : node._children[i + 1];
                newNode.ChildrenMask = node.ChildrenMask ^ (1u << h);
                return newNode;
            }
            
            newNode._children = node._children.ToArray();
            newNode.ChildrenMask = node.ChildrenMask;
            newNode._children[pos] = value;
            return newNode;
        }
    }
}