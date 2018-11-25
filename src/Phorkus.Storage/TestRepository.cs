using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using Phorkus.Proto;
using Phorkus.RocksDB;

namespace Phorkus.Storage
{
    public class TestRepository<TKey, TValue>
    {
        private readonly IRocksDbContext _rocksDbContext;

        public TestRepository(IRocksDbContext rocksDbContext)
        {
            _rocksDbContext = rocksDbContext ?? throw new ArgumentNullException(nameof(rocksDbContext));
        }

        public PersistentTreeMapNode<PersistentTreeMap, TKey, TValue> GetNode(PersistentTreeMap id)
        {
            return _GetBlockByHash(id);
        }

        public bool WriteNode(PersistentTreeMap id, PersistentTreeMapNode<PersistentTreeMap, TKey, TValue> node)
        {
            byte[] bytes;
            using (MemoryStream stream = new MemoryStream())
            {
                IFormatter formatter = new BinaryFormatter();
                formatter.Serialize(stream, node);
                bytes = stream.ToArray();
            }

            _rocksDbContext.Save(EntryPrefix.BlockByHash.BuildPrefix(id.Id), bytes);
            return true;
        }

        private PersistentTreeMapNode<PersistentTreeMap, TKey, TValue> _GetBlockByHash(PersistentTreeMap id)
        {
            var prefix = EntryPrefix.TestPrefix.BuildPrefix(id.Id);
            var raw = _rocksDbContext.Get(prefix);
            if (raw == null)
                return null;
            PersistentTreeMapNode<PersistentTreeMap, TKey, TValue> parsed;
            using (MemoryStream stream = new MemoryStream(raw))
            {
                IFormatter formatter = new BinaryFormatter();
                parsed = (PersistentTreeMapNode<PersistentTreeMap, TKey, TValue>) formatter.Deserialize(stream);
            }

            return parsed;
        }
    }
}