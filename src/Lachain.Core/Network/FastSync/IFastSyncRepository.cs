using System.Collections.Generic;
using Lachain.Core.Blockchain.Checkpoints;
using Lachain.Proto;
using Lachain.Storage;
using Lachain.Storage.Trie;


namespace Lachain.Core.Network.FastSync
{
    public interface IFastSyncRepository
    {
        void Initialize(ulong blockNumber, UInt256 blockHash, List<(UInt256, CheckpointType)> stateHashes);
        ulong GetSavedBatch();
        ulong GetTotalIncomingBatch();
        void UpdateSavedBatch(ulong savedBatch);
        void SaveIncomingQueueBatch(List<byte> incomingQueue, ulong totalIncomingBatch);
        byte[] GetHashBatchRaw(ulong batch);
        bool TryAddNode(TrieNodeInfo nodeInfo);
        bool ExistNode(UInt256 nodeHash);
        bool GetIdByHash(UInt256 nodeHash, out ulong id);
        bool TryGetNode(ulong id, out IHashTrieNode? trieNode);
        bool IsConsistent(TrieNodeInfo? node, out UInt256? nodeHash);
        ulong GetCheckpointBlockNumber();
        UInt256? GetCheckpointBlockHash();
        UInt256? GetCheckpointStateHash(CheckpointType checkpointType);
        ulong GetCurrentBlockHeight();
        void AddBlock(Block block);
        Block? BlockByHeight(ulong height);
        void CommitIds();
        void Commit();
        int GetLastDownloadedTries();
        void SetLastDownloadedTries(int downloaded);
        VersionFactory GetVersionFactory();
        void SetSnapshotVersion(string trieName, UInt256 rootHash);
        void SetState();
    }
}