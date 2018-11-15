namespace Phorkus.Core.Storage
{
    public interface IGlobalRepository
    {
        /// <summary>
        /// Retrieves the total / current block height
        /// </summary>
        /// <returns>Total / current block height</returns>
        uint GetTotalBlockHeight();
        
        /// <summary>
        /// Set the total/ current block height
        /// </summary>
        /// <param name="height">Total / current block height</param>
        void SetTotalBlockHeight(uint height);
        
        /// <summary>
        /// Retrieves the total / current block header height
        /// </summary>
        /// <returns>Total / current block header height</returns>
        uint GetTotalBlockHeaderHeight();

        /// <summary>
        /// Set the total/ current block header height
        /// </summary>
        /// <param name="height">Total / current block header height</param>
        void SetTotalBlockHeaderHeight(uint height);
    }
}