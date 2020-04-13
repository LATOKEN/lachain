using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Lachain.Crypto;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Proto;
using Lachain.Utility.Utils;
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

        public void NewValidators(IEnumerable<ECDSAPublicKey> publicKeys)
        {
            _state.AddOrUpdate(
                EntryPrefix.PendingValidators.BuildPrefix(),
                publicKeys.Select(x => x.EncodeCompressed()).Flatten().ToArray()
            );
        }

        public int ConfirmCredentials(PublicKeySet tsKeys, PublicKey tpkePublicKey)
        {
            var keyringHash = tpkePublicKey.ToBytes().Concat(tsKeys.ToBytes()).Keccak();
            var dbKey = EntryPrefix.ConfirmationMessage.BuildPrefix(keyringHash);
            var rawValue = _state.Get(dbKey);
            var votes = rawValue != null ? BitConverter.ToInt32(rawValue) : 0;
            _state.AddOrUpdate(dbKey, BitConverter.GetBytes(votes + 1));
            return votes + 1;
        }

        public void UpdateValidators(PublicKeySet tsKeys, PublicKey tpkePublicKey)
        {
            var state = new ConsensusState {TpkePublicKey = ByteString.CopyFrom(tpkePublicKey.ToBytes())};
            var ecdsaPublicKeys = _state.Get(EntryPrefix.PendingValidators.BuildPrefix())
                .Batch(CryptoUtils.PublicKeyLength)
                .Select(x => x.ToArray().ToPublicKey());
            state.Validators.AddRange(ecdsaPublicKeys.Zip(tsKeys.Keys, (ecdsaKey, tsKey) => new ValidatorCredentials
            {
                PublicKey = ecdsaKey,
                ResolvableAddress = "",
                ThresholdSignaturePublicKey = ByteString.CopyFrom(tsKey.ToBytes())
            }));
            SetConsensusState(state);
            // TODO: how to clear confirmations?
        }
    }

    public class ConsensusStateNotPresentException : Exception
    {
    }
}