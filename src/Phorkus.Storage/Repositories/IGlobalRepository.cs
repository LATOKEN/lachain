using Phorkus.Proto;

namespace Phorkus.Storage.Repositories
{
    public interface IGlobalRepository
    {
        /// <summary>
        /// Retrieves the total / current block height
        /// </summary>
        /// <returns>Total / current block height</returns>
        ulong GetTotalBlockHeight();
        
        /// <summary>
        /// Set the total/ current block height
        /// </summary>
        /// <param name="height">Total / current block height</param>
        void SetTotalBlockHeight(ulong height);
        
        /// <summary>
        /// Retrieves the total / current block header height
        /// </summary>
        /// <returns>Total / current block header height</returns>
        ulong GetTotalBlockHeaderHeight();

        /// <summary>
        /// Set the total/ current block header height
        /// </summary>
        /// <param name="height">Total / current block header height</param>
        void SetTotalBlockHeaderHeight(ulong height);

        bool GenesisBlockExists();
        
        ThresholdKey GetShare();
        
        void SetShare(ThresholdKey thresholdKey);

        ulong GetBlockchainHeight(BlockchainType blockchainType);

        ulong SetBlockchainHeight(BlockchainType blockchainType);
    }
}