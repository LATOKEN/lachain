namespace Lachain.Storage.Trie
{
    internal class ReplacedNode
    {
        public ulong NodeId ;
        public ulong ReplacerId ;

        public ReplacedNode(ulong nodeId , ulong replacerId)
        {
            NodeId = nodeId ;
            ReplacerId = replacerId ;
        }
    }
}
