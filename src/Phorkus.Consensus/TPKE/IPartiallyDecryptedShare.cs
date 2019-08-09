using System;
using System.Collections.Generic;

namespace Phorkus.Consensus.TPKE
{
    public interface IPartiallyDecryptedShare : IEquatable<IEncryptedShare>, IComparable<IEncryptedShare>
    {
        int Id { get; }
    }
    
}