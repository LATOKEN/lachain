using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Proto;
using Lachain.Utility;
using Lachain.Utility.Serialization;
using PublicKey = Lachain.Crypto.TPKE.PublicKey;

namespace Lachain.Storage.State
{
    public class ValidatorSnapshot : IValidatorSnapshot
    {
        private readonly IStorageState _state;

        public ValidatorSnapshot(IStorageState state)
        {
            _state = state;
        }

        // public ulong Version => _state.CurrentVersion;
        public ulong Version
        {
            get
            {
                return _state.CurrentVersion;
            }
            set
            {
                _state.CurrentVersion = value;
            }
        }

        public void Commit()
        {
            // Console.WriteLine($"Validator Commit: {_state.Hash}");
            _state.Commit();
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
    }

    public class ConsensusStateNotPresentException : Exception
    {
    }
}