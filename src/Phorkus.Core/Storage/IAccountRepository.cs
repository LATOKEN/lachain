using Phorkus.Core.Proto;

namespace Phorkus.Core.Storage
{
    public interface IAccountRepository
    {
        /// <summary>
        /// Finds user account by address
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        Account GetAccountByAddress(UInt160 address);

        /// <summary>
        /// Finds or created account by address
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        Account GetAccountByAddressOrDefault(UInt160 address);

        void ChangeBalance(UInt160 address, UInt160 asset, UInt256 delta);
        
        /// <summary>
        /// Deletes accounts from database
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        void DeleteAccountByAddress(UInt160 address);
        
        /// <summary>
        /// Adds or updates account in storage
        /// </summary>
        /// <param name="account"></param>
        /// <returns></returns>
        Account AddAccount(Account account);
    }
}