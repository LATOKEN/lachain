using Phorkus.Proto;
using Phorkus.Utility;

namespace Phorkus.Storage.State
{
    public interface IBalanceSnapshot : ISnapshot
    {
        Money GetAvailableBalance(UInt160 owner);
        void SetAvailableBalance(UInt160 owner, Money value);
        
        Money AddAvailableBalance(UInt160 owner, Money value);
        Money SubAvailableBalance(UInt160 owner, Money value);
        
        bool TransferAvailableBalance(UInt160 from, UInt160 to, Money value);
    }
}