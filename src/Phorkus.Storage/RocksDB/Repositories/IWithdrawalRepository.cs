using System.Collections.Generic;
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
        
        /// <summary>
        /// Adds a transaction to the repository
        /// </summary>
        /// <param name="withdrawal">Withdrawal to add</param>
        void AddWithdrawal(Withdrawal withdrawal);
        
        /// <summary>
        /// Removes a withdrawal from the repository
        /// </summary>
        /// <param name="withdrawal">Withdrawal to remove</param>
        void RemoveWithdrawal(Withdrawal withdrawal);

        void AddWithdrawalState(Withdrawal withdrawal);
        
        Withdrawal GetWithdrawalByStateNonce(WithdrawalState withdrawalState, ulong nonce);
        

        WithdrawalState ChangeWithdrawalState(UInt256 withdrawalHash, WithdrawalState withdrawalState);
        Withdrawal ChangeWithdrawal(UInt256 withdrawalHash, Withdrawal withdrawal);

        WithdrawalState GetWithdrawalState(UInt256 withdrawalHash);
        
        IEnumerable<Withdrawal> GetWithdrawalsByHashes(IEnumerable<UInt256> withdrawalHashes);
        IEnumerable<Withdrawal> GetWithdrawalsByNonces(IEnumerable<ulong> nonces);

        Withdrawal GetWithdrawalByNonce(ulong nonce);

        bool ContainsWithdrawalByHash(UInt256 withdrawalHash);

        Withdrawal GetWithdrawalByNonceAndDelete(ulong nonce);
    }
}