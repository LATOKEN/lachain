using System.Collections.Generic;
using Lachain.Proto;
using Lachain.Storage;
using Lachain.Storage.Repositories;
using Lachain.Storage.State;
using Lachain.Storage.Trie;

namespace Lachain.Core.Network.FastSynchronizerBatch
{
    public interface IFastSyncRepository
    {
        void Initialize(ulong blockNumber, UInt256 blockHash, List<(UInt256, CheckpointType)> stateHashes);
        bool TryAddNode(TrieNodeInfo nodeInfo);
        bool ExistNode(UInt256 nodeHash);
        bool GetIdByHash(UInt256 nodeHash, out ulong id);
        bool TryGetNode(ulong id, out IHashTrieNode? trieNode);
        bool IsConsistent(TrieNodeInfo? node, out UInt256? nodeHash);
        ulong GetBlockNumber();
        UInt256? GetBlockHash();
        UInt256? GetStateHash(CheckpointType checkpointType);
        ulong GetBlockHeight();
        void AddBlock(Block block);
        void CommitIds();
        void Commit();
        int GetLastDownloadedTries();
        void SetLastDownloadedTries(int downloaded);
        VersionFactory GetVersionFactory();
        void SetSnapshotVersion(string trieName, UInt256 rootHash);
        void SetState();
    }
}