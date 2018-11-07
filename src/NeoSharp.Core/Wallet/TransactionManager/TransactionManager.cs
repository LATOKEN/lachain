using System;
using System.Collections.Generic;
using NeoSharp.Core.Models;
using NeoSharp.Core.Persistence;
using NeoSharp.Types;

namespace NeoSharp.Core.Wallet.TransactionManager
{
    // assetId, unspent list
    using UnspentCoinsDictionary = Dictionary<UInt256, List<CoinReference>>;

    public class TransactionManager : ITransactionManager
    {
        IRepository _repository;

        public TransactionManager(IRepository repository)
        {
            _repository = repository;
        }

        /// <inheritdoc />
        public ContractTransaction BuildContractTransaction(
            IWalletAccount from,
            TransactionAttribute[] attributes,
            TransactionOutput[] outputs)
        {
            //TODO #396: Complete transaction
            return new ContractTransaction();
        }


        /// <inheritdoc />
        public ContractTransaction BuildContractTransaction(
            TransactionAttribute[] attributes,
            CoinReference[] inputs,
            TransactionOutput[] outputs)
        {
            //TODO #396: Complete transaction
            return new ContractTransaction();
        }

        /// <inheritdoc />
        public ContractTransaction BuildContractTransaction(IWallet from, TransactionAttribute[] attributes,
            TransactionOutput[] outputs)
        {
            return new ContractTransaction();
        }
        
        public UnspentCoinsDictionary GetBalance(IWalletAccount from)
        {
            throw new NotImplementedException();
        }

        public UInt256 BroadcastTransaction(IWalletAccount account, Transaction transaction)
        {
            throw new NotImplementedException();
        }
    }
}