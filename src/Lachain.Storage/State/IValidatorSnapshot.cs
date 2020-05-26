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

        void UpdateValidators(IEnumerable<ECDSAPublicKey> ecdsaKeys, PublicKeySet tsKeys, PublicKey tpkePublicKey);
    }
}