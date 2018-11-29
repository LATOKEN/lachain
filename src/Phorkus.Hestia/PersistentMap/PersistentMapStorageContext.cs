using Phorkus.RocksDB;

namespace Phorkus.Hestia.PersistentMap
{
    public class PersistentMapStorageContext
    {
        private readonly IRocksDbContext _rocksDbContext;

        public PersistentMapStorageContext(IRocksDbContext rocksDbContext)
        {
            _rocksDbContext = rocksDbContext;
        }

        public PersistentMapNode GetNode(ulong id)
        {
            var prefix = EntryPrefix.PersistentTreeMap.BuildPrefix(id);
            var raw = _rocksDbContext.Get(prefix);
            return PersistentMapNode.FromBytes(raw);
        }

        public void WriteNode(ulong id, PersistentMapNode node)
        {
            var prefix = EntryPrefix.PersistentTreeMap.BuildPrefix(id);
            _rocksDbContext.Save(prefix, node.ToByteArray());
        }
    }
}