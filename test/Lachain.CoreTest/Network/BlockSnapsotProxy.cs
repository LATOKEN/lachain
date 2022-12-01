using System.Collections.Generic;
using Lachain.Proto;
using Lachain.Storage;
using Lachain.Storage.DbCompact;
using Lachain.Storage.State;
using Lachain.Storage.Trie;

namespace Lachain.CoreTest.Network
{
    public class BlockSnapsotProxy: IBlockSnapshot
    {
        public ulong Version { get; }
        public uint RepositoryId { get; }
        private uint blockCount { get; set; }
        public void Commit(RocksDbAtomicWrite batch)
        {
            throw new System.NotImplementedException();
        }

        public UInt256 Hash { get; }
        public IDictionary<ulong, IHashTrieNode> GetState()
        {
            throw new System.NotImplementedException();
        }

        public bool IsTrieNodeHashesOk()
        {
            throw new System.NotImplementedException();
        }

        public ulong SetState(ulong root, IDictionary<ulong, IHashTrieNode> allTrieNodes)
        {
            throw new System.NotImplementedException();
        }

        public void SetCurrentVersion(ulong root)
        {
            throw new System.NotImplementedException();
        }

        public void ClearCache()
        {
            throw new System.NotImplementedException();
        }

        public ulong SaveNodeId(IDbShrinkRepository _repo)
        {
            throw new System.NotImplementedException();
        }

        public ulong GetTotalBlockHeight()
        {
            return blockCount;
        }

        public Block? GetBlockByHeight(ulong blockHeight)
        {
            throw new System.NotImplementedException();
        }

        public Block? GetBlockByHash(UInt256 blockHash)
        {
            throw new System.NotImplementedException();
        }

        public void AddBlock(Block block)
        {
            blockCount++;
        }

        public IEnumerable<Block> GetBlocksByHeightRange(ulong height, ulong count)
        {
            throw new System.NotImplementedException();
        }

        public IEnumerable<Block> GetBlocksByHashes(IEnumerable<UInt256> hashes)
        {
            throw new System.NotImplementedException();
        }
    }
}