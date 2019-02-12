using Phorkus.Proto;
using Phorkus.Storage.State;
using Phorkus.Utility;
using Phorkus.Utility.Utils;
using Phorkus.Core.VM;

namespace Phorkus.Core.Blockchain.OperationManager.TransactionManager
{
    public class ContractTransactionExecuter : ITransactionExecuter
    {
        private readonly IVirtualMachine _virtualMachine;

        public ContractTransactionExecuter(IVirtualMachine virtualMachine)
        {
            _virtualMachine = virtualMachine;
        }

        public OperatingError Execute(Block block, Transaction transaction, IBlockchainSnapshot snapshot)
        {
            /* validate transaction before execution */
            var error = Verify(transaction);
            if (error != OperatingError.Ok)
                return error;
            var contract = transaction.Contract;
            /* try to transfer funds from sender to recipient */
            if (!contract.Value.IsZero() && !snapshot.Balances.TransferAvailableBalance(transaction.From, contract.To,
                    contract.Asset, new Money(contract.Value)))
                return OperatingError.InsufficientBalance;
            /* if we have invocation block than invoke contract method */
            if (contract.Input != null && !contract.Input.IsEmpty)
                return _InvokeContract(transaction, snapshot);
            return OperatingError.Ok;
        }

        private OperatingError _InvokeContract(Transaction transaction, IBlockchainSnapshot snapshot)
        {
            var contract = snapshot.Contracts.GetContractByHash(transaction.Contract.To);
            if (contract is null)
                return OperatingError.ContractNotFound;
            return _virtualMachine.InvokeContract(contract, transaction.From, transaction.Contract.Input.ToByteArray()) != ExecutionStatus.Ok
                ? OperatingError.ContractFailed
                : OperatingError.Ok;
        }

        public OperatingError Verify(Transaction transaction)
        {
            if (transaction.Type != TransactionType.Contract)
                return OperatingError.InvalidTransaction;
            var contract = transaction.Contract;
            if (contract?.Asset is null || contract.Asset.IsZero())
                return OperatingError.InvalidTransaction;
            if (contract.To is null)
                return OperatingError.InvalidTransaction;
            if (contract.Value is null)
                return OperatingError.InvalidTransaction;
            return _VerifyInvocation(transaction);
        }

        private static OperatingError _VerifyInvocation(Transaction transaction)
        {
            /* TODO: "verify invocation input here" */
            return OperatingError.Ok;
        }
    }
}