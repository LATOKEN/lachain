namespace Lachain.Storage.Trie
{
    internal class NodeRepository
    {
        private readonly IRocksDbContext _rocksDbContext;

        public NodeRepository(IRocksDbContext rocksDbContext)
        {
            _rocksDbContext = rocksDbContext;
        }

        public IHashTrieNode GetNode(ulong id)
        {
            var prefix = EntryPrefix.PersistentHashMap.BuildPrefix(id);
            var raw = _rocksDbContext.Get(prefix);
            return NodeSerializer.FromBytes(raw);
        }

        public void WriteNode(ulong id, IHashTrieNode node)
        {
            var prefix = EntryPrefix.PersistentHashMap.BuildPrefix(id);
            _rocksDbContext.Save(prefix, NodeSerializer.ToByteArray(node));
        }
    }
}