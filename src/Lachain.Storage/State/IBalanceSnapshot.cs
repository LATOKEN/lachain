using Lachain.Proto;
using Lachain.Utility;

namespace Lachain.Storage.State
{
    public interface IBalanceSnapshot : ISnapshot
    {
        Money GetBalance(UInt160 owner);
        Money GetSupply();
        
        bool TransferBalance(UInt160 from, UInt160 to, Money value, TransactionReceipt receipt, bool checkSignature, bool useNewChainId);
        /// <summary>
        /// Transfers <c>value</c> where <c>allowance</c> was approved from sender.
        /// Plain address balance transfer should not use this method.
        /// Check if <c>allowance</c> was approved from sender before using this method.
        /// </summary>
        /// <returns><c>true</c> if transferred, <c>false</c> otherwise</returns>
        bool TransferAllowance(UInt160 from, UInt160 to, Money value, Money allowance);
        
        Money GetAllowedSupply();
        void SetAllowedSupply(Money value);
        UInt160 GetMinter();
        void SetMinter(UInt160 minter);
        /// <summary>
        /// Mints LaToken by increasing balance of <c>address</c>.
        /// Total supply is also increased.
        /// Should not be called unless token is supposed to be minted.
        /// </summary>
        /// <returns>updated balance of <c>address</c></returns>
        Money MintLaToken(UInt160 address, Money value);
        Money RemoveCollectedFees(Money fee, TransactionReceipt receipt);
        /// <summary>
        /// Transfers balance from a contract to an address.
        /// Plain address balance transfer should not call this method
        /// </summary>
        /// <returns><c>true</c> if transferred successfully, <c>false</c> otherwise</returns>
        bool TransferContractBalance(UInt160 from, UInt160 to, Money value);
        /// <summary>
        /// Transfers balance from a system contract to an address.
        /// Plain address balance transfer should not call this method.
        /// This methods should only be used in System contracts
        /// </summary>
        /// <returns><c>true</c> if transferred successfully, <c>false</c> otherwise</returns>
        bool TransferSystemContractBalance(UInt160 from, UInt160 to, Money value, TransactionReceipt receipt, bool checkVerification);

    }
}