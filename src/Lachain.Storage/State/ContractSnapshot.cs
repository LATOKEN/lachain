﻿using System.Runtime.CompilerServices;
using Lachain.Proto;
using Lachain.Utility;
using Lachain.Storage.Trie;
using System.Collections.Generic;
using Lachain.Storage.DbCompact;

namespace Lachain.Storage.State
{
    /*
        Contract Snapshot is HMAT (trie) which acts as an key-value storage to store the following infos: 
        (1) (contractAddress -> byte code of the contract)

    */
    public class ContractSnapshot : IContractSnapshot
    {
        private readonly IStorageState _state;

        public ContractSnapshot(IStorageState state)
        {
            _state = state;
        }

        public IDictionary<ulong,IHashTrieNode> GetState()
        {
            return _state.GetAllNodes();
        }
        
        public bool IsTrieNodeHashesOk()
        {
            return _state.IsNodeHashesOk();
        }
        public ulong SetState(ulong root, IDictionary<ulong, IHashTrieNode> allTrieNodes)
        {
            return _state.InsertAllNodes(root, allTrieNodes);
        }
        
        public ulong Version => _state.CurrentVersion;

        public uint RepositoryId => _state.RepositoryId;
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Commit(RocksDbAtomicWrite batch)
        {
            _state.Commit(batch);
        }

        public UInt256 Hash => _state.Hash;

        [MethodImpl(MethodImplOptions.Synchronized)]
        public Contract? GetContractByHash(UInt160 contractHash)
        {
            var value = _state.Get(EntryPrefix.ContractByHash.BuildPrefix(contractHash));
            return value != null ? Contract.FromBytes(value) : null;
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void AddContract(UInt160 sender, Contract contract)
        {
            _state.AddOrUpdate(EntryPrefix.ContractByHash.BuildPrefix(contract.ContractAddress), contract.ToBytes());
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void DeleteContractByHash(UInt160 contractHash)
        {
            _state.Delete(EntryPrefix.ContractByHash.BuildPrefix(contractHash), out _);
        }
        public void SetCurrentVersion(ulong root)
        {
            _state.SetCurrentVersion(root);
        }
        public void ClearCache()
        {
            _state.ClearCache();
        }

        public ulong SaveNodeId(IDbShrinkRepository _repo)
        {
            return _state.SaveNodeId(_repo);
        }

    }
}