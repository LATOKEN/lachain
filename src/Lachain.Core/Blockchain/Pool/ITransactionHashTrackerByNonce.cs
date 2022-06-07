using Lachain.Proto;

namespace Lachain.Core.Blockchain.Pool
{
    public interface ITransactionHashTrackerByNonce
    {
        /// <summary>
        /// Given the transaction receipt, tries to add the hash in cache
        /// The mapping is (address,nonce) => hash
        /// </summary>
        /// <param name="receipt">Transaction receipt</param>
        /// <returns><c>True</c> if added successfully, <c>False</c> otherwise</returns>
        bool TryAdd(TransactionReceipt receipt);
        /// <summary>
        /// Given the transaction receipt, tries to remove the hash from cache
        /// </summary>
        /// <param name="receipt">Transaction receipt</param>
        /// <returns><c>True</c> if removed successfully, <c>False</c> otherwise</returns>
        bool TryRemove(TransactionReceipt receipt);
        /// <summary>
        /// Gets the transaction <c>hash</c> for the given <c>address</c> and <c>nonce</c>
        /// </summary>
        /// <param name="address">Address of the sender of the transaction to search</param>
        /// <param name="nonce">Nonce of the transaction to search</param>
        /// <param name="hash">Hash of the transaction to search</param>
        /// <returns><c>True</c> if found such transaction, <c>False</c> otherwise</returns>
        bool TryGetTransactionHash(UInt160 address, ulong nonce, out UInt256? hash);
    }
}