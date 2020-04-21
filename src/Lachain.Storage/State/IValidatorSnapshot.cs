using System.Collections.Generic;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Proto;
using PublicKey = Lachain.Crypto.TPKE.PublicKey;

namespace Lachain.Storage.State
{
    public interface IValidatorSnapshot : ISnapshot
    {
        ConsensusState GetConsensusState();

        void SetConsensusState(ConsensusState consensusState);

        IEnumerable<ECDSAPublicKey> GetValidatorsPublicKeys();

        void NewValidators(IEnumerable<ECDSAPublicKey> publicKeys);

        int ConfirmCredentials(PublicKeySet tsKeys, PublicKey tpkePublicKey);

        void UpdateValidators(PublicKeySet tsKeys, PublicKey tpkePublicKey);
    }
}