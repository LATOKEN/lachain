using System;
using System.Collections.Generic;
using System.Linq;
using NeoSharp.BinarySerialization;
using NeoSharp.Core;
using NeoSharp.Core.Models;
using NeoSharp.Core.Storage.Blockchain;

namespace NeoSharp.RocksDB.Repositories
{
    public class BlockRepository : IBlockRepository
    {
        private readonly IBinarySerializer _binarySerializer;
        private readonly IRocksDbContext _rocksDbContext;
        
        public BlockRepository(
            IRocksDbContext rocksDbContext,
            IBinarySerializer binarySerializer)
        {
            _binarySerializer = binarySerializer ?? throw new ArgumentNullException(nameof(binarySerializer));
            _rocksDbContext = rocksDbContext ?? throw new ArgumentNullException(nameof(rocksDbContext));
        }

        public BlockHeader GetBlockHeaderByHeight(uint blockHeight)
        {
            return _GetBlockHeaderByHeight(blockHeight);
        }
        
        public BlockHeader GetBlockHeaderByHash(UInt256 blockHash)
        {
            return _GetBlockHeaderByHash(blockHash);
        }

        public IEnumerable<UInt256> GetBlockHashesByHeights(IEnumerable<uint> heights)
        {
            var prefixes = heights.Select(h => EntryPrefix.BlockHashByHeight.BuildPrefix(h));
            var hashes = _rocksDbContext.GetMany(prefixes);
            return hashes.Values.Where(h => h != null && h.Length == UInt256.Zero.Size).Select(h => new UInt256(h));
        }

        public bool AddBlockHeader(BlockHeader blockHeader)
        {
            /* validate block before addition */
            if (!_IsBlockValid(blockHeader))
                return false;
            /* write block by hash */
            _rocksDbContext.Save(EntryPrefix.BlockByHash.BuildPrefix(blockHeader.Hash), _binarySerializer.Serialize(blockHeader));
            /* write block hash by height */
            _rocksDbContext.Save(EntryPrefix.BlockHashByHeight.BuildPrefix(blockHeader.Index), _binarySerializer.Serialize(blockHeader.Hash));
            return true;
        }
        
        public BlockHeader GetNextBlockHeaderByHash(UInt256 blockHash)
        {
            var block = _GetBlockHeaderByHash(blockHash);
            if (block == null)
                return null;
            return _GetBlockHeaderByHeight(block.Index + 1);
        }
        
        public IEnumerable<BlockHeader> GetBlockHeaderByHashes(IEnumerable<UInt256> hashes)
        {
            var prefixes = hashes.Select(h => EntryPrefix.BlockByHash.BuildPrefix(h));
            var result = _rocksDbContext.GetMany(prefixes);
            return result.Values.Where(b => b != null).Select(b => _binarySerializer.Deserialize<BlockHeader>(b));
        }

        public IEnumerable<BlockHeader> GetBlockHeadersByHeightRange(uint height, uint count)
        {
            var prefixes = Enumerable.Range((int) height, (int) count).Select(
                h => EntryPrefix.BlockHashByHeight.BuildPrefix((uint) h));
            var hashes = _rocksDbContext.GetMany(prefixes).Values.Where(h => h != null && h.Length == UInt256.Zero.Size).Select(h => new UInt256(h));
            return GetBlockHeaderByHashes(hashes);
        }
        
        private BlockHeader _GetBlockHeaderByHeight(uint blockHeight)
        {
            var prefix = EntryPrefix.BlockHashByHeight.BuildPrefix(blockHeight);
            var raw = _rocksDbContext.Get(prefix);
            if (raw == null)
                return null;
            var hash = _binarySerializer.Deserialize<UInt256>(raw);
            return hash == null ? null : _GetBlockHeaderByHash(hash);
        }

        private BlockHeader _GetBlockHeaderByHash(UInt256 blockHash)
        {
            var prefix = EntryPrefix.BlockByHash.BuildPrefix(blockHash);
            var raw = _rocksDbContext.Get(prefix);
            if (raw == null)
                return null;
            var blockHeader = _binarySerializer.Deserialize<BlockHeader>(raw);
            return blockHeader;
        }

        private bool _IsBlockValid(BlockHeader blockHeader)
        {
            if (blockHeader.Hash == null || blockHeader.Hash.Size == 0)
                return false;
            return true;
        }
    }
}