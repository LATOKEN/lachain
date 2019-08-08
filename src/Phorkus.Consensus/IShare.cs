using System;
using System.Collections.Generic;

namespace Phorkus.Consensus
{
    public interface IShare : IEquatable<IShare>, IComparable<IShare>
    {
    }
}