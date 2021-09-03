using System.Collections.Generic;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Proto;
using Lachain.Utility;
using PublicKey = Lachain.Crypto.TPKE.PublicKey;

namespace Lachain.Storage.State
{
    public interface IValidatorSnapshot : ISnapshot
    {
        ConsensusState GetConsensusState();

        void SetConsensusState(ConsensusState consensusState);

        IEnumerable<ECDSAPublicKey> GetValidatorsPublicKeys();

        void UpdateValidators(IEnumerable<ECDSAPublicKey> ecdsaKeys, PublicKeySet tsKeys, PublicKey tpkePublicKey);

        void SetStakerAddress(UInt160 address);

        UInt160 GetStakerAddress();
    }
}