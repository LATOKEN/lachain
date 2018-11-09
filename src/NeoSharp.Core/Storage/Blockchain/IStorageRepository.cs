using System.Threading.Tasks;
using NeoSharp.Core.Models;

namespace NeoSharp.Core.Storage.Blockchain
{
    public interface IStorageRepository
    {
        /// <summary>
        /// Retrieves a StorageValue by its StorageKey
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        Task<StorageValue> GetStorage(StorageKey key);

        /// <summary>
        /// Adds a StorageValue
        /// </summary>
        /// <param name="key"></param>
        /// <param name="val"></param>
        /// <returns></returns>
        Task AddStorage(StorageKey key, StorageValue val);
        
        /// <summary>
        /// Deletes a StorageKey and its associated StorageValue
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        Task DeleteStorage(StorageKey key);
    }
}