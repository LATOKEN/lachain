using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Phorkus.Proto;

namespace Phorkus.Storage.State
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
            var raw = _state.Get(EntryPrefix.ConsensusState.BuildPrefix());
            return raw != null ? ConsensusState.Parser.ParseFrom(raw) : null;
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
    }
}