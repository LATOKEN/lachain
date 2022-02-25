﻿using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Proto;
using Lachain.Utility;
using Lachain.Utility.Serialization;
using PublicKey = Lachain.Crypto.TPKE.PublicKey;
using Lachain.Storage.Trie;

namespace Lachain.Storage.State
{
    public class ValidatorSnapshot : IValidatorSnapshot
    {
        private readonly IStorageState _state;

        public ValidatorSnapshot(IStorageState state)
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

        public void Commit(RocksDbAtomicWrite batch)
        {
            _state.Commit(batch);
        }

        public UInt256 Hash => _state.Hash;

        public ConsensusState GetConsensusState()
        {
            var raw = _state.Get(EntryPrefix.ConsensusState.BuildPrefix()) ??
                      throw new ConsensusStateNotPresentException();
            return ConsensusState.FromBytes(raw);
        }

        public void SetConsensusState(ConsensusState consensusState)
        {
            var raw = consensusState.ToBytes();
            _state.AddOrUpdate(EntryPrefix.ConsensusState.BuildPrefix(), raw);
        }

        public IEnumerable<ECDSAPublicKey> GetValidatorsPublicKeys()
        {
            return GetConsensusState().Validators.Select(v => v.PublicKey);
        }

        public void UpdateValidators(
            IEnumerable<ECDSAPublicKey> ecdsaKeys, PublicKeySet tsKeys, PublicKey tpkePublicKey
        )
        {
            var state = new ConsensusState(
                tpkePublicKey.ToBytes(),
                ecdsaKeys
                    .Zip(tsKeys.Keys, (ecdsaKey, tsKey) => new ValidatorCredentials(ecdsaKey, tsKey.ToBytes()))
                    .ToArray()
            );
            SetConsensusState(state);
        }
        public void SetCurrentVersion(ulong root)
        {
            _state.SetCurrentVersion(root);
        }
        public void ClearCache()
        {
            _state.ClearCache();
        }

        public ulong UpdateNodeIdToBatch(bool save, RocksDbAtomicWrite batch)
        {
            return _state.UpdateNodeIdToBatch(save, batch);
        }

        public ulong DeleteSnapshot(ulong block, RocksDbAtomicWrite batch)
        {
            return _state.DeleteOldNodes(block, batch);
        }
    }

    public class ConsensusStateNotPresentException : Exception
    {
    }
}