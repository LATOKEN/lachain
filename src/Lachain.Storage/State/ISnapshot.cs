﻿using Lachain.Proto;
using Lachain.Storage.Trie;
using System.Collections.Generic;

namespace Lachain.Storage.State
{
    public interface ISnapshot
    {
        ulong Version { get; }
        uint RepositoryId { get; }
        void Commit(RocksDbAtomicWrite batch);
        UInt256 Hash { get; }
        IDictionary<ulong,IHashTrieNode> GetState();
        bool IsTrieNodeHashesOk();

        ulong SetState(ulong root, IDictionary<ulong, IHashTrieNode> allTrieNodes);

        void SetCurrentVersion(ulong root);

        void ClearCache();

        ulong UpdateNodeIdToBatch(bool save, RocksDbAtomicWrite batch);

        ulong DeleteSnapshot(ulong block, RocksDbAtomicWrite batch);
    }
}