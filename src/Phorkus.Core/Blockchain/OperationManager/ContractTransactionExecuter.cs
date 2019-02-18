using System;
using Phorkus.Core.Blockchain.ContractManager;
using Phorkus.Core.VM;
using Phorkus.Proto;
using Phorkus.Storage.State;
using Phorkus.Utility;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.Blockchain.OperationManager
{
    public class ContractTransactionExecuter : ITransactionExecuter
    {
        private readonly IContractRegisterer _contractRegisterer;
        private readonly IVirtualMachine _virtualMachine;

        public ContractTransactionExecuter(
            IContractRegisterer contractRegisterer,
            IVirtualMachine virtualMachine)
        {
            _contractRegisterer = contractRegisterer;
            _virtualMachine = virtualMachine;
        }

        public OperatingError Execute(Block block, Transaction transaction, IBlockchainSnapshot snapshot)
        {
            /* validate transaction before execution */
            var error = Verify(transaction);
            if (error != OperatingError.Ok)
                return error;
            /* try to transfer funds from sender to recipient */
            if (!transaction.Value.IsZero() && !snapshot.Balances.TransferBalance(transaction.From, transaction.To,
                    new Money(transaction.Value)))
                return OperatingError.InsufficientBalance;
            /* if we have invocation block than invoke contract method */
            if (transaction.Invocation != null && !transaction.Invocation.IsEmpty)
                return _InvokeContract(transaction, snapshot);
            return OperatingError.Ok;
        }

        private OperatingError _InvokeContract(Transaction transaction, IBlockchainSnapshot snapshot)
        {
            var systemContract = _contractRegisterer.GetContractByAddress(transaction.To);
            if (systemContract != null)
                return _InvokeSystemContract(transaction, snapshot);
            var contract = snapshot.Contracts.GetContractByHash(transaction.To);
            if (contract is null)
                return OperatingError.ContractNotFound;
            return _virtualMachine.InvokeContract(contract, transaction.From, transaction.Invocation.ToByteArray()) !=
                   ExecutionStatus.Ok
                ? OperatingError.ContractFailed
                : OperatingError.Ok;
        }

        private OperatingError _InvokeSystemContract(Transaction transaction, IBlockchainSnapshot snapshot)
        {
            var result = _contractRegisterer.DecodeContract(transaction.To, transaction.Invocation.ToByteArray());
            if (result is null)
                return OperatingError.ContractFailed;
            var (contract, method, args) = result;
            try
            {
                var context = new ContractContext
                {
                    Snapshot = snapshot,
                    Sender = transaction.From,
                    Transaction = transaction
                };
                var inst = Activator.CreateInstance(contract, context);
                method.Invoke(inst, args);
            }
            catch (NotSupportedException e)
            {
                Console.Error.WriteLine(e);
                return OperatingError.ContractFailed;
            }
            catch (InvalidOperationException e)
            {
                Console.Error.WriteLine(e);
                return OperatingError.ContractFailed;
            }
            return OperatingError.Ok;
        }

        public OperatingError Verify(Transaction transaction)
        {
            if (transaction.Type != TransactionType.Transfer)
                return OperatingError.InvalidTransaction;
            if (transaction.To is null)
                return OperatingError.InvalidTransaction;
            if (transaction.Value is null)
                return OperatingError.InvalidTransaction;
            if (!transaction.Deploy.IsEmpty)
                return OperatingError.InvalidTransaction;
            return _VerifyInvocation(transaction);
        }

        private OperatingError _VerifyInvocation(Transaction transaction)
        {
            /* TODO: "verify invocation input here" */
            return OperatingError.Ok;
        }
    }
}