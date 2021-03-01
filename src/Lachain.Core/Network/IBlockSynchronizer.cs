using System;
using System.Collections.Generic;
using Lachain.Proto;
using Lachain.Storage;
using Lachain.Storage.Repositories;
using Lachain.Storage.Trie;

namespace Lachain.Core.Network
{
    public interface IBlockSynchronizer : IDisposable
    {
        uint WaitForTransactions(IEnumerable<UInt256> transactionHashes, TimeSpan timeout);

        uint HandleTransactionsFromPeer(IEnumerable<TransactionReceipt> transactions, ECDSAPublicKey publicKey);
        
        void HandlePeerHasBlocks(ulong blockHeight, ECDSAPublicKey publicKey);

        bool HandleBlockFromPeer(BlockInfo block, ECDSAPublicKey publicKey);
        
        ulong? GetHighestBlock();
        
        IDictionary<ECDSAPublicKey, ulong> GetConnectedPeers();

        void SynchronizeWith(IEnumerable<ECDSAPublicKey> peers);

        void Start();
        
        void PerformFastSync();
        
        (bool, ulong) GetFastSyncDetail();
        List<string> GetRpcPeers();

        void SetRpcPeers(List<string> peers);
        
        void SetNodeForPersist(Dictionary<ulong, Tuple<List<ulong>, List<byte[]>>> repoBlocks);

        void PersistNodesForFastSync(NodeRepository nodeRepository, RocksDbAtomicWrite rocksDbAtomicWrite);

        ulong GetMetaVersion();

        void SetMetaVersion(VersionRepository versionRepository, RocksDbAtomicWrite rocksDbAtomicWrite, ulong meta);
    }
}