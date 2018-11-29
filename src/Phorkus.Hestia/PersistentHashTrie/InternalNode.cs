namespace Phorkus.Hestia.PersistentHashTrie
{
    public class InternalNode : IHashTrieNode
    {
        private uint _childrenMask;
        private ulong[] _children;
        
        public NodeType Type { get; } = NodeType.Internal;

        private uint Popcount(uint x)
        {
            
            x -= x >> 1 & 0x55555555;
            x = (x & 0x33333333) + (x >> 2 & 0x33333333);
            x = x + (x >> 4) & 0x0f0f0f0f;
            x += x >> 8;
            x += x >> 16;
            return x & 0x7f;
        }
        
        public ulong GetChildByHash(byte h)
        {
            
        }

        public static InternalNode ModifyChildren(InternalNode node, byte h, ulong value)
        {
            
            return null;
        }
    }
}