using System.Collections.Generic;

namespace Phorkus.Core.Design
{
    public interface IContract
    {
        UInt160 Address { get; }
        ICollection<byte> Code { get; }
        UInt160 Deployer { get; }
    }
}