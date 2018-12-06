using System.Collections.Generic;
using System.Linq;
using Phorkus.Proto;
using Phorkus.Core.Storage;
using Phorkus.Core.Utils;
using Phorkus.Utility;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.Blockchain.OperationManager.TransactionManager
{
    public class ContractTransactionPersister : ITransactionPersister
    {
        private readonly IBalanceRepository _balanceRepository;

        public ContractTransactionPersister(IBalanceRepository balanceRepository)
        {
            _balanceRepository = balanceRepository;
        }
        
        public OperatingError Execute(Block block, Transaction transaction)
        {
            var error = Verify(transaction);
            if (error != OperatingError.Ok)
                return error;
            var contract = transaction.Contract;
            if (!contract.Value.IsZero())
                _balanceRepository.TransferBalance(transaction.From, contract.To, contract.Asset, new Money(contract.Value));
            /* TODO: "invoke smart-contract code here" */
            return OperatingError.Ok;
        }
        
        public OperatingError Verify(Transaction transaction)
        {
            if (transaction.Type != TransactionType.Contract)
                return OperatingError.InvalidTransaction;
            var contract = transaction.Contract;
            if (contract is null)
                return OperatingError.InvalidTransaction;
            if (contract.Asset is null || contract.Asset.IsZero())
                return OperatingError.InvalidTransaction;
            if (contract.To is null)
                return OperatingError.InvalidTransaction;
            if (contract.Value is null)
                return OperatingError.InvalidTransaction;
            return _VerifyScript(contract.Script);
        }
        
        private OperatingError _VerifyScript(IEnumerable<byte> script)
        {
            if (script is null || !script.Any())
                return OperatingError.Ok;
            /* TODO: "validate opcodes here" */
            return OperatingError.InvalidTransaction;
        }
    }
}