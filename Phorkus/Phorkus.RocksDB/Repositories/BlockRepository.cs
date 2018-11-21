using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Phorkus.Core.Proto;
using Phorkus.Core.Storage;
using Phorkus.Core.Utils;

namespace Phorkus.RocksDB.Repositories
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
            _rocksDbContext.Save(EntryPrefix.BlockByHash.BuildPrefix(block.ToHash256()), block.ToByteArray());
            /* write block hash by height */
            _rocksDbContext.Save(EntryPrefix.BlockHashByHeight.BuildPrefix(block.Header.Index), block.ToHash256().ToByteArray());
            return true;
        }
        
        public Block GetNextBlockByHash(UInt256 blockHash)
        {
            var block = _GetBlockByHash(blockHash);
            if (block == null)
                return null;
            return _GetBlockByHeight(block.Header.Index + 1);
        }
        
        public IEnumerable<Block> GetBlocksByHeightRange(uint height, uint count)
        {
            var prefixes = Enumerable.Range((int) height, (int) count).Select(
                h => EntryPrefix.BlockHashByHeight.BuildPrefix((uint) h));
            var hashes = _rocksDbContext.GetMany(prefixes).Values.Where(h => h != null).Select(UInt256.Parser.ParseFrom);
            return GetBlocksByHashes(hashes);
        }
        
        public IEnumerable<Block> GetBlocksByHashes(IEnumerable<UInt256> hashes)
        {
            var prefixes = hashes.Select(h => EntryPrefix.BlockByHash.BuildPrefix(h));
            var result = _rocksDbContext.GetMany(prefixes);
            return result.Values.Where(b => b != null).Select(Block.Parser.ParseFrom);
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