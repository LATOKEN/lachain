using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Proto;
using Lachain.Utility;
using Lachain.Utility.Serialization;
using PublicKey = Lachain.Crypto.TPKE.PublicKey;
using Lachain.Storage.Trie;
using Lachain.Storage.DbCompact;

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

        public void SetConsensusState(ConsensusState consensusState, bool useNewFormat)
        {
            var raw = consensusState.ToBytes(useNewFormat);
            _state.AddOrUpdate(EntryPrefix.ConsensusState.BuildPrefix(), raw);
        }

        public IEnumerable<ECDSAPublicKey> GetValidatorsPublicKeys()
        {
            return GetConsensusState().Validators.Select(v => v.PublicKey);
        }

        public void UpdateValidators(
            IEnumerable<ECDSAPublicKey> ecdsaKeys, PublicKeySet tsKeys, PublicKey tpkePublicKey, IEnumerable<PublicKey> tpkeVerificationKeys, bool useNewFormat
        )
        {
            var state = new ConsensusState(
                tpkePublicKey.ToBytes(),
                tpkeVerificationKeys.Select(x => x.ToBytes()).ToArray(),
                ecdsaKeys
                    .Zip(tsKeys.Keys, (ecdsaKey, tsKey) => new ValidatorCredentials(ecdsaKey, tsKey.ToBytes()))
                    .ToArray()
            );
            SetConsensusState(state, useNewFormat);
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

    public class ConsensusStateNotPresentException : Exception
    {
    }
}