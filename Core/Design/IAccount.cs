using System.Collections.Generic;

namespace Core.Design
{
    public interface IAccount
    {
        UInt160 Address { get; }
        IDictionary<UInt160, UInt256> Balances { get; }
    }
}