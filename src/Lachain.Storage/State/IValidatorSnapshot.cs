using System.Collections.Generic;
using Lachain.Proto;

namespace Lachain.Storage.State
{
    public interface IValidatorSnapshot : ISnapshot
    {
        ConsensusState GetConsensusState();
        
        void SetConsensusState(ConsensusState consensusState);

        IEnumerable<ECDSAPublicKey> GetValidatorsPublicKeys();
    }
}