using System.Collections.Generic;
using Phorkus.Consensus;
using Phorkus.Proto;

namespace Phorkus.Core.Blockchain.Validators
{
    public interface IValidatorManager
    {
        IPublicConsensusKeySet GetValidators(long afterBlock);
        IReadOnlyCollection<ECDSAPublicKey> GetValidatorsPublicKeys(long afterBlock);
        int GetValidatorIndex(ECDSAPublicKey publicKey, long afterBlock);
        ECDSAPublicKey GetPublicKey(uint validatorIndex, long afterBlock);
    }
}