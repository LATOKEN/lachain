using System.Collections.Generic;
using Lachain.Consensus;
using Lachain.Proto;

namespace Lachain.Core.Blockchain.Validators
{
    public interface IValidatorManager
    {
        IPublicConsensusKeySet? GetValidators(long afterBlock);
        IReadOnlyCollection<ECDSAPublicKey> GetValidatorsPublicKeys(long afterBlock);
        int GetValidatorIndex(ECDSAPublicKey publicKey, long afterBlock);
        ECDSAPublicKey GetPublicKey(uint validatorIndex, long afterBlock);
        bool IsValidatorForBlock(ECDSAPublicKey key, long block);
    }
}