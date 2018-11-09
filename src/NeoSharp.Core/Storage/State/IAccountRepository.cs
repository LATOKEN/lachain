using System.Threading.Tasks;
using NeoSharp.Core.Models;
using NeoSharp.Types;

namespace NeoSharp.Core.Storage.State
{
    public interface IAccountRepository
    {
        /// <summary>
        /// Finds user account by address
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        Task<Account> GetAccountByAddress(UInt160 address);

        /// <summary>
        /// Finds or created account by address
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        Task<Account> GetOrCreateAccountByAddress(UInt160 address);

        /// <summary>
        /// Deletes accounts from database
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        Task DeleteAccountByAddress(UInt160 address);
    }
}