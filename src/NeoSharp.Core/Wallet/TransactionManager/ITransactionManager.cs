using System.Collections.Generic;
using NeoSharp.Core.Models;
using NeoSharp.Types;

namespace NeoSharp.Core.Wallet.TransactionManager
{
    public interface ITransactionManager 
    {
        /// <summary>
        /// Builds the contract transaction.
        /// This is a very common kind of transaction as it allows one wallet to send NEO to another. 
        /// The inputs and outputs transaction fields will usually be important for this transaction 
        /// (for example, to govern how much NEO will be sent, and to what address).
        /// In this method the input will be gathered from the account unspent balance.
        /// </summary>
        /// <returns>The contract transaction.</returns>
        /// <param name="from">From.</param>
        /// <param name="attributes">Attributes.</param>
        /// <param name="outputs">Outputs.</param>
        ContractTransaction BuildContractTransaction(IWalletAccount from, TransactionAttribute[] attributes, TransactionOutput[] outputs);

        /// <summary>
        /// Builds the contract transaction.
        /// This is a very common kind of transaction as it allows one wallet to send NEO to another. 
        /// The inputs and outputs transaction fields will usually be important for this transaction 
        /// (for example, to govern how much NEO will be sent, and to what address).
        /// In this method the input will be gathered from all wallet accounts unspent balance.
        /// </summary>
        /// <returns>The contract transaction.</returns>
        /// <param name="from">From.</param>
        /// <param name="attributes">Attributes.</param>
        /// <param name="outputs">Outputs.</param>
        ContractTransaction BuildContractTransaction(IWallet from, TransactionAttribute[] attributes, TransactionOutput[] outputs);

        /// <summary>
        /// Builds the contract transaction.
        /// This is a very common kind of transaction as it allows one wallet to send NEO to another. 
        /// The inputs and outputs transaction fields will usually be important for this transaction 
        /// (for example, to govern how much NEO will be sent, and to what address).
        /// </summary>
        /// <returns>The contract transaction.</returns>
        /// <param name="attributes">Attributes.</param>
        /// <param name="inputs">Inputs.</param>
        /// <param name="outputs">Outputs.</param>
        ContractTransaction BuildContractTransaction(TransactionAttribute[] attributes, CoinReference[] inputs, TransactionOutput[] outputs);
        
        /// <summary>
        /// Send the transaction over the network
        /// </summary>
        /// <returns>Transaction Hash</returns>
        UInt256 BroadcastTransaction(IWalletAccount account, Transaction transaction);

        /// <summary>
        /// Get Wallet Balance
        /// </summary>
        /// <returns>The unspent coins.</returns>
        /// <param name="from">From.</param>
        Dictionary<UInt256, List<CoinReference>> GetBalance(IWalletAccount from);
    }
}
