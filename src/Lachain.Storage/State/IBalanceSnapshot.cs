using Lachain.Proto;
using Lachain.Utility;

namespace Lachain.Storage.State
{
    public interface IBalanceSnapshot : ISnapshot
    {
        Money GetBalance(UInt160 owner);
        Money GetSupply();
        
        bool TransferBalance(UInt160 from, UInt160 to, Money value, TransactionReceipt receipt, bool checkSignature, bool useNewChainId);
        
        Money GetAllowedSupply();
        void SetAllowedSupply(Money value);
        UInt160 GetMinter();
        void SetMinter(UInt160 minter);
        Money MintLaToken(UInt160 address, Money value);

    }
}