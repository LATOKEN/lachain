using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;

namespace Lachain.Storage.State
{
    public class BlockSnapshot : IBlockSnapshot
    {
        private readonly IStorageState _state;
        private static readonly ILogger<BlockSnapshot> Logger = LoggerFactory.GetLoggerForClass<BlockSnapshot>();

        public BlockSnapshot(IStorageState state)
        {
            _state = state;
        }

        public ulong Version => _state.CurrentVersion;

        public void Commit()
        {
            _state.Commit();
        }

        public UInt256 Hash => _state.Hash;

        public ulong GetTotalBlockHeight()
        {
            var raw = _state.Get(EntryPrefix.BlockHeight.BuildPrefix());
            return raw?.AsReadOnlySpan().ToUInt64() ?? 0u;
        }

        public Block? GetBlockByHeight(ulong blockHeight)
        {
            var raw = _state.Get(EntryPrefix.BlockHashByHeight.BuildPrefix(blockHeight));
            if (raw is null)
                return null;
            var blockHash = UInt256.Parser.ParseFrom(raw);
            return GetBlockByHash(blockHash);
        }

        public Block? GetBlockByHash(UInt256 blockHash)
        {
            var raw = _state.Get(EntryPrefix.BlockByHash.BuildPrefix(blockHash));
            return raw != null ? Block.Parser.ParseFrom(raw) : null;
        }

        public void AddBlock(Block block)
        {
            var currentHeight = GetTotalBlockHeight();
            if (block.Header.Index != 0 && block.Header.Index != currentHeight + 1)
                throw new Exception(
                    $"Invalid block height, expected {currentHeight + 1}, but got {block.Header.Index}");
            _state.Add(EntryPrefix.BlockByHash.BuildPrefix(block.Hash), block.ToByteArray());
            _state.Add(EntryPrefix.BlockHashByHeight.BuildPrefix(block.Header.Index), block.Hash.ToByteArray());
            _state.AddOrUpdate(EntryPrefix.BlockHeight.BuildPrefix(), block.Header.Index.ToBytes().ToArray());
        }

        public IEnumerable<Block> GetBlocksByHeightRange(ulong height, ulong count)
        {
            var result = new List<Block>();
            for (var i = height; i < height + count; i++)
            {
                if (i > GetTotalBlockHeight())
                    break;
                var block = GetBlockByHeight(i);
                if (block is null)
                {
                    Logger.LogWarning($"No block for requested height {i}, current height is {GetTotalBlockHeight()}");
                    continue;
                }
                if (UInt256Utils.IsZero(block.Header.StateHash))
                {
                    Logger.LogError($"Requested block for height {i} has zero state hash, current height is {GetTotalBlockHeight()}");
                    continue;
                }
                result.Add(block);
            }

            return result;
        }

        public IEnumerable<Block> GetBlocksByHashes(IEnumerable<UInt256> hashes)
        {
            return hashes.SelectMany(hash =>
            {
                var block = GetBlockByHash(hash);
                return block is null ? Enumerable.Empty<Block>() : new[] {block};
            });
        }
    }
}