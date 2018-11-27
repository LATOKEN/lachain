using System.Collections.Generic;
using Phorkus.Proto;

namespace Phorkus.Core.Blockchain
{
    public interface IValidatorManager
    {
        IReadOnlyCollection<PublicKey> Validators { get; }

        uint Quorum { get; }
    }
}