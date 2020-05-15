using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Proto;
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

        public ulong Version => _state.CurrentVersion;

        public void Commit()
        {
            _state.Commit();
        }

        public UInt256 Hash => _state.Hash;

        public ConsensusState GetConsensusState()
        {
            var raw = _state.Get(EntryPrefix.ConsensusState.BuildPrefix()) ??
                      throw new ConsensusStateNotPresentException();
            return ConsensusState.Parser.ParseFrom(raw);
        }

        public void SetConsensusState(ConsensusState consensusState)
        {
            var raw = consensusState.ToByteArray();
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
            var state = new ConsensusState {TpkePublicKey = ByteString.CopyFrom(tpkePublicKey.ToBytes())};
            state.Validators.AddRange(ecdsaKeys.Zip(tsKeys.Keys, (ecdsaKey, tsKey) => new ValidatorCredentials
            {
                PublicKey = ecdsaKey,
                ResolvableAddress = "",
                ThresholdSignaturePublicKey = ByteString.CopyFrom(tsKey.ToBytes())
            }));
            SetConsensusState(state);
        }
    }

    public class ConsensusStateNotPresentException : Exception
    {
    }
}