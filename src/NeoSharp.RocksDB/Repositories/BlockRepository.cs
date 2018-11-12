using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NeoSharp.BinarySerialization;
using NeoSharp.Core.Models;
using NeoSharp.Core.Storage.Blockchain;
using NeoSharp.Types;

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

        public async Task<BlockHeader> GetBlockHeaderByHeight(uint blockHeight)
        {
            var rawBlockHash = await _rocksDbContext.Get(blockHeight.BuildIxHeightToHashKey());
            if (rawBlockHash == null)
                return null;
            var blockHash = _binarySerializer.Deserialize<UInt256>(rawBlockHash);
            var rawHeader = await _rocksDbContext.Get(blockHash.BuildDataBlockKey());
            return rawHeader == null ? null : _binarySerializer.Deserialize<BlockHeader>(rawHeader);
        }

        public async Task<BlockHeader> GetBlockHeaderByHash(UInt256 blockHash)
        {
            var rawHeader = await _rocksDbContext.Get(blockHash.BuildDataBlockKey());
            return rawHeader == null ? null : _binarySerializer.Deserialize<BlockHeader>(rawHeader);
        }

        public async Task<IEnumerable<UInt256>> GetBlockHashesByHeights(IEnumerable<uint> heights)
        {
            var heightsHashes = await _rocksDbContext.GetMany(heights.Select(h => h.BuildIxHeightToHashKey()));
            return heightsHashes.Values.Where(h => h != null && h.Length == UInt256.Zero.Size)
                .Select(h => new UInt256(h));
        }

        public async Task AddBlockHeader(BlockHeader blockHeader)
        {
            await _rocksDbContext.Save(blockHeader.Hash.BuildDataBlockKey(), _binarySerializer.Serialize(blockHeader));
            await _rocksDbContext.Save(blockHeader.Index.BuildIxHeightToHashKey(), blockHeader.Hash.ToArray());
        }

        public async Task<BlockHeader> GetNextBlockHeaderByHash(UInt256 blockHash)
        {
            throw new NotImplementedException();
        }
        
        public async Task<IEnumerable<BlockHeader>> GetBlockHeaderByHashes(IReadOnlyCollection<UInt256> hashes)
        {
            throw new NotImplementedException();
        }

        public async Task<IEnumerable<BlockHeader>> GetBlockHeadersFromHeight(uint height, uint count)
        {
            throw new NotImplementedException();
        }
    }
}