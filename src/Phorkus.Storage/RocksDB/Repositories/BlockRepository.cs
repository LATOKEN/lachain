using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Phorkus.Proto;

namespace Phorkus.Storage.RocksDB.Repositories
{
    public class BlockRepository : IBlockRepository
    {
        private readonly IRocksDbContext _rocksDbContext;
        
        public BlockRepository(IRocksDbContext rocksDbContext)
        {
            _rocksDbContext = rocksDbContext ?? throw new ArgumentNullException(nameof(rocksDbContext));
        }
        
        public Block GetBlockByHeight(ulong blockHeight)
        {
            return _GetBlockByHeight(blockHeight);
        }
        
        public Block GetBlockByHash(UInt256 blockHash)
        {
            return _GetBlockByHash(blockHash);
        }

        public IEnumerable<UInt256> GetBlockHashesByHeights(IEnumerable<uint> heights)
        {
            var prefixes = heights.Select(h => EntryPrefix.BlockHashByHeight.BuildPrefix(h));
            var hashes = _rocksDbContext.GetMany(prefixes);
            return hashes.Values.Where(h => h != null).Select(UInt256.Parser.ParseFrom);
        }
        
        public bool AddBlock(Block block)
        {
            /* write block by hash */
            _rocksDbContext.Save(EntryPrefix.BlockByHash.BuildPrefix(block.Hash), block.ToByteArray());
            /* write block hash by height */
            _rocksDbContext.Save(EntryPrefix.BlockHashByHeight.BuildPrefix(block.Header.Index), block.Hash.ToByteArray());
            return true;
        }
        
        public Block GetNextBlockByHash(UInt256 blockHash)
        {
            var block = _GetBlockByHash(blockHash);
            if (block == null)
                return null;
            return _GetBlockByHeight(block.Header.Index + 1);
        }
        
        public IEnumerable<Block> GetBlocksByHeightRange(ulong height, ulong count)
        {
            var result = new List<Block>();
            for (var i = height; i < height + count; i++)
            {
                var block = _GetBlockByHeight(i);
                if (block is null)
                    continue;
                result.Add(block);
            }

            return result;
        }
        
        public IEnumerable<Block> GetBlocksByHashes(IEnumerable<UInt256> hashes)
        {
            return hashes.Select(_GetBlockByHash).Where(block => block != null);
        }
        
        private Block _GetBlockByHeight(ulong blockHeight)
        {
            var prefix = EntryPrefix.BlockHashByHeight.BuildPrefix(blockHeight);
            var raw = _rocksDbContext.Get(prefix);
            if (raw == null)
                return null;
            var hash = UInt256.Parser.ParseFrom(raw);
            return hash == null ? null : _GetBlockByHash(hash);
        }

        private Block _GetBlockByHash(UInt256 blockHash)
        {
            var prefix = EntryPrefix.BlockByHash.BuildPrefix(blockHash);
            var raw = _rocksDbContext.Get(prefix);
            if (raw == null)
                return null;
            var block = Block.Parser.ParseFrom(raw);
            return block;
        }
    }
}