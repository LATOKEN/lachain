using System;
using System.Linq;
using Google.Protobuf;
using Phorkus.Proto;
using Phorkus.RocksDB;
using Phorkus.Storage.Mappings;
using Phorkus.Storage.Treap;

namespace Phorkus.Storage.Repositories
{
    public class BlockRepository : IPersistentMapRepository<UInt256, Block>
    {
        private readonly IRocksDbContext _rocksDbContext;

        public BlockRepository(IRocksDbContext rocksDbContext)
        {
            _rocksDbContext = rocksDbContext;
        }

        public PersistentTreeMapNode<UInt256, Block> GetNode(IPersistentTreeMap id)
        {
            var prefix = EntryPrefix.BlockByHash.BuildPrefix(id.Id);
            var raw = _rocksDbContext.Get(prefix);
            return raw == null ? null : FromBytes(raw);
        }

        public bool WriteNode(IPersistentTreeMap id, PersistentTreeMapNode<UInt256, Block> node)
        {
            _rocksDbContext.Save(EntryPrefix.BlockByHash.BuildPrefix(id.Id), ToByteArray(node));
            return true;
        }

        private static byte[] ToByteArray(PersistentTreeMapNode<UInt256, Block> node)
        {
            var left = BitConverter.GetBytes(node.LeftSon.Id);
            var right = BitConverter.GetBytes(node.RightSon.Id);
            var key = node.Key.ToByteArray();
            var keyLen = BitConverter.GetBytes(key.Length);
            var value = node.Value.ToByteArray();
            return left.Concat(right).Concat(keyLen).Concat(key).Concat(value).ToArray();
        }

        private static PersistentTreeMapNode<UInt256, Block> FromBytes(byte[] bytes)
        {
            var leftId = BitConverter.ToUInt64(bytes, 0);
            var rightId = BitConverter.ToUInt64(bytes, 8);
            var len = BitConverter.ToInt32(bytes, 16);
            var key = UInt256.Parser.ParseFrom(bytes, 20, len);
            var value = Block.Parser.ParseFrom(bytes, 20 + len, bytes.Length - 20 - len);
            return new PersistentTreeMapNode<UInt256, Block>(new BlockMap(leftId), new BlockMap(rightId), key, value);
        }
    }
}