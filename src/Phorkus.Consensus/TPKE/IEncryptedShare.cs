using System;
using System.Collections.Generic;

namespace Phorkus.Consensus.TPKE
{
    public interface IEncryptedShare : IEquatable<IEncryptedShare>, IComparable<IEncryptedShare>
    {
        int Id { get; }

        byte[] ToBytes();
    }
}