using Lachain.Proto;
using Lachain.Utility;

namespace Lachain.Storage.State
{
    public interface IBalanceSnapshot : ISnapshot
    {
        Money GetBalance(UInt160 owner);
        void SetBalance(UInt160 owner, Money value);
        
        Money AddBalance(UInt160 owner, Money value);
        Money SubBalance(UInt160 owner, Money value);
        
        bool TransferBalance(UInt160 from, UInt160 to, Money value);
    }
}