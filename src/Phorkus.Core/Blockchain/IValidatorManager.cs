using System.Collections.Generic;
using Phorkus.Proto;

namespace Phorkus.Core.Blockchain
{
    public interface IValidatorManager
    {
        IReadOnlyCollection<ECDSAPublicKey> Validators { get; }

        uint Quorum { get; }

        ECDSAPublicKey GetPublicKey(uint validatorIndex);
        
        uint GetValidatorIndex(ECDSAPublicKey publicKey);
        
        bool CheckValidator(UInt160 address);
        
        bool CheckValidator(ECDSAPublicKey publicKey);
    }
}