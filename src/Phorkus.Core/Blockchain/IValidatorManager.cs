using System.Collections.Generic;
using Phorkus.Proto;

namespace Phorkus.Core.Blockchain
{
    public interface IValidatorManager
    {
        IReadOnlyCollection<PublicKey> Validators { get; }

        uint Quorum { get; }

        PublicKey GetPublicKey(uint validatorIndex);
        
        uint GetValidatorIndex(PublicKey publicKey);
        
        bool CheckValidator(UInt160 address);
        
        bool CheckValidator(PublicKey publicKey);
    }
}