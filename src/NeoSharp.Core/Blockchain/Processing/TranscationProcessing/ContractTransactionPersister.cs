using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NeoSharp.Core.Models.Transactions;
using NeoSharp.Core.Storage.State;

namespace NeoSharp.Core.Blockchain.Processing.TranscationProcessing
{
    public class ContractTransactionPersister : ITransactionPersister<ContractTransaction>
    {
        private readonly IAccountRepository _accountRepository;
        
        public ContractTransactionPersister(IAccountRepository accountRepository)
        {
            _accountRepository = accountRepository;
        }
        
        public async Task Persist(ContractTransaction transaction)
        {
            await _transferFunds(transaction.Asset, transaction.From, transaction.Value, true);
            await _transferFunds(transaction.Asset, transaction.To, transaction.Value, false);
        }
        
        private async Task _transferFunds(UInt160 asset, UInt160 address, UInt256 value, bool negate)
        {
            var account = _accountRepository.GetAccountByAddressOrDefault(address);
            var balance = account.Balances.GetValueOrDefault(asset, UInt256.Zero);
            if (balance < value)
                throw new ArgumentException(nameof(value));
            if (negate)
                balance -= value;
            else
                balance += value;
            account.Balances.Add(asset, balance);
            _accountRepository.AddAccount(account);
        }
    }
}