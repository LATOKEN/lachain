using Phorkus.Proto;

namespace Phorkus.Storage.RocksDB.Repositories
{
    public interface IWithdrawalRepository
    {
        /// <summary>
        /// Retrieves a withdrawal by identifier / hash
        /// </summary>
        /// <param name="withdrawalHash">Identifier / hash of the withdrawal</param>
        /// <returns>Withdrawal with the specified id / hash</returns>
        Withdrawal GetWithdrawalByHash(UInt256 withdrawalHash);
        
        bool ContainsWithdrawalByHash(UInt256 withdrawalHash);
        
        /// <summary>
        /// Retreives a withdrawal by nonce
        /// </summary>
        /// <param name="nonce">Nonce for withdrawal</param>
        /// <returns></returns>
        Withdrawal GetWithdrawalByNonce(ulong nonce);
        
        bool ContainsWithdrawalByNonce(ulong nonce);
        
        /// <summary>
        /// Adds a transaction to the repository
        /// </summary>
        /// <param name="withdrawal">Withdrawal to add</param>
        bool AddWithdrawal(Withdrawal withdrawal);
        
        Withdrawal ChangeWithdrawalState(UInt256 withdrawalHash, WithdrawalState withdrawalState);

        bool ApproveWithdrawal(UInt256 withdrawalHash, byte[] rawTransaction, byte[] transactionHash);
        
        Withdrawal GetWithdrawalByNonceAndDelete(ulong nonce);

        ulong GetCurrentWithdrawalNonce();

        ulong GetApprovedWithdrawalNonce();
    }
}