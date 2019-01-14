using Phorkus.Proto;
using Phorkus.Utility;

namespace Phorkus.Storage.State
{
    public interface IBalanceSnapshot : ISnapshot
    {
        Money GetAvailableBalance(UInt160 owner, UInt160 asset);
        void SetAvailableBalance(UInt160 owner, UInt160 asset, Money value);
        
        Money AddAvailableBalance(UInt160 owner, UInt160 asset, Money value);
        Money SubAvailableBalance(UInt160 owner, UInt160 asset, Money value);
        
        Money GetWithdrawingBalance(UInt160 owner, UInt160 asset);
        void SetWithdrawingBalance(UInt160 owner, UInt160 asset, Money value);

        Money AddWithdrawingBalance(UInt160 owner, UInt160 asset, Money value);
        Money SubWithdrawingBalance(UInt160 owner, UInt160 asset, Money value);
        
        void TransferAvailableBalance(UInt160 from, UInt160 to, UInt160 asset, Money value);
    }
}