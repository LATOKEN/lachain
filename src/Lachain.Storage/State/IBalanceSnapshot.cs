using Lachain.Proto;
using Lachain.Utility;

namespace Lachain.Storage.State
{
    public interface IBalanceSnapshot : ISnapshot
    {
        Money GetBalance(UInt160 owner);
        Money GetSupply();
        void SetBalance(UInt160 owner, Money value);
        
        Money AddBalance(UInt160 owner, Money value, bool increaseSupply = false);
        Money SubBalance(UInt160 owner, Money value);
        
        bool TransferBalance(UInt160 from, UInt160 to, Money value);
        
        Money GetAllowedSupply();
        void SetAllowedSupply(Money value);
        UInt160 GetMinter();
        void SetMinter(UInt160 minter);

        void AddToTouch(TransactionReceipt receipt);

        void TouchAll();
    }
}