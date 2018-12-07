namespace Phorkus.Core.Blockchain.State
{
    public interface IStorageSnapshot
    {
        /// <summary>
        /// Retrieves a StorageValue by its StorageKey
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        StorageValue GetStorage(StorageKey key);

        /// <summary>
        /// Adds a StorageValue
        /// </summary>
        /// <param name="key"></param>
        /// <param name="val"></param>
        /// <returns></returns>
        void AddOrUpdateStorage(StorageKey key, StorageValue val);
        
        /// <summary>
        /// Deletes a StorageKey and its associated StorageValue
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        void DeleteStorage(StorageKey key);
    }
}