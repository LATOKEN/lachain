using System;
using System.Collections.Generic;
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

        public OperatingError Execute(Block block, TransactionReceipt receipt, IBlockchainSnapshot snapshot)
        {
            /* check gas limit */
            var error = _CheckGasLimit(receipt);
            if (error != OperatingError.Ok)
                return error;
            var transaction = receipt.Transaction;
            /* validate transaction before execution */
            error = Verify(transaction);
            if (error != OperatingError.Ok)
                return error;
            /* try to transfer funds from sender to recipient */
            if (!transaction.Value.IsZero() &&
                !snapshot.Balances.TransferBalance(transaction.From, transaction.To, new Money(transaction.Value)))
                return OperatingError.InsufficientBalance;
            /* if we have invocation block than invoke contract method */
            if (transaction.Invocation != null && !transaction.Invocation.IsEmpty)
                return _InvokeContract(block, receipt, snapshot);
            return OperatingError.Ok;
        }

        private OperatingError _InvokeContract(Block block, TransactionReceipt receipt, IBlockchainSnapshot snapshot)
        {
            var transaction = receipt.Transaction;
            var systemContract = _contractRegisterer.GetContractByAddress(transaction.To);
            if (systemContract != null)
                return _InvokeSystemContract(transaction, snapshot);
            var contract = snapshot.Contracts.GetContractByHash(transaction.To);
            if (contract is null)
                return OperatingError.ContractNotFound;
            var input = transaction.Invocation.ToByteArray();
            if (_IsConstructorCall(input))
                return OperatingError.InvalidInput;
            var context = new InvocationContext(transaction.From, transaction, block);
            try
            {
                var status = _virtualMachine.InvokeContract(contract, context, input);
                return status != ExecutionStatus.Ok ? OperatingError.ContractFailed : OperatingError.Ok;
            }
            catch (OutOfGasException e)
            {
                receipt.GasUsed += e.GasUsed;
            }
            return OperatingError.OutOfGas;
        }

        private OperatingError _CheckGasLimit(TransactionReceipt receipt)
        {
            const ulong inputDataGas = 100;
            if (receipt.Transaction.Invocation.IsEmpty)
                return OperatingError.Ok;
            receipt.GasUsed += (ulong) receipt.Transaction.Invocation.Length * inputDataGas;
            return receipt.GasUsed > receipt.Transaction.GasLimit ? OperatingError.OutOfGas : OperatingError.Ok;
        }

        private bool _IsConstructorCall(IReadOnlyList<byte> buffer)
        {
            if (buffer.Count < 4)
                return false;
            return buffer[0] == 0 &&
                   buffer[1] == 0 &&
                   buffer[2] == 0 &&
                   buffer[3] == 0;
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