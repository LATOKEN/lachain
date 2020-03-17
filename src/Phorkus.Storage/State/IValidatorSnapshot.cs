using System.Collections.Generic;
using Phorkus.Proto;

namespace Phorkus.Storage.State
{
    public interface IValidatorSnapshot : ISnapshot
    {
        ConsensusState GetConsensusState();

        IEnumerable<ECDSAPublicKey> GetValidatorsPublicKeys();
    }
}