using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Core.Blockchain.ContractManager;
using Lachain.Core.VM;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Utility;
using Lachain.Utility.Utils;

namespace Lachain.Core.Blockchain.OperationManager
{
    public class ContractTransactionExecuter : ITransactionExecuter
    {
        private readonly IContractRegisterer _contractRegisterer;
        private readonly IVirtualMachine _virtualMachine;
        public event EventHandler<ContractContext>? OnSystemContractInvoked;

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
            if (block.Header.Index == 0) // genesis is special case, just mint tokens
            {
                if (!receipt.Transaction.From.Equals(UInt160Utils.Zero)) return OperatingError.InvalidTransaction;
                if (!receipt.Transaction.Invocation.IsEmpty) return OperatingError.InvalidTransaction;
                snapshot.Balances.AddBalance(receipt.Transaction.To, transaction.Value.ToMoney());
                return OperatingError.Ok;
            }

            var contract = snapshot.Contracts.GetContractByHash(transaction.To);
            var systemContract = _contractRegisterer.GetContractByAddress(transaction.To);
            if (contract is null && systemContract is null)
            {
                /*
                 * Destination address is not smart-contract, just plain address
                 * So we just call transfer method of system contract
                 */
                if (snapshot.Balances.GetBalance(transaction.From) < new Money(transaction.Value))
                    return OperatingError.InsufficientBalance;
                var invocation = ContractEncoder.Encode("transfer(address,uint256)", transaction.To, transaction.Value);
                return _InvokeSystemContract(ContractRegisterer.LatokenContract, invocation, receipt, snapshot);
            }

            /* try to transfer funds from sender to recipient */
            if (!snapshot.Balances.TransferBalance(transaction.From, transaction.To, new Money(transaction.Value)))
                return OperatingError.InsufficientBalance;
            /* invoke required function or fallback */
            return _InvokeContract(block, receipt, snapshot);
        }

        private OperatingError _InvokeContract(Block block, TransactionReceipt receipt, IBlockchainSnapshot snapshot)
        {
            var transaction = receipt.Transaction;
            var systemContract = _contractRegisterer.GetContractByAddress(transaction.To);
            if (systemContract != null)
                return _InvokeSystemContract(transaction.To, transaction.Invocation.ToArray(), receipt, snapshot);
            var contract = snapshot.Contracts.GetContractByHash(transaction.To);
            if (contract is null) return OperatingError.ContractFailed; // this should not happen
            var input = transaction.Invocation.ToByteArray();
            if (_IsConstructorCall(input))
                return OperatingError.InvalidInput;
            var context = new InvocationContext(transaction.From, transaction, block);
            try
            {
                if (receipt.GasUsed > transaction.GasLimit)
                    return OperatingError.OutOfGas;
                var result =
                    _virtualMachine.InvokeContract(contract, context, input, transaction.GasLimit - receipt.GasUsed);
                if (result.Status != ExecutionStatus.Ok)
                    return OperatingError.ContractFailed;
                receipt.GasUsed += result.GasUsed;
                return receipt.GasUsed > transaction.GasLimit ? OperatingError.OutOfGas : OperatingError.Ok;
            }
            catch (OutOfGasException e)
            {
                receipt.GasUsed += e.GasUsed;
            }

            return OperatingError.OutOfGas;
        }

        private OperatingError _CheckGasLimit(TransactionReceipt receipt)
        {
            const ulong inputDataGas = 10;
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

        private OperatingError _InvokeSystemContract(
            UInt160 address, byte[] invocation, TransactionReceipt receipt, IBlockchainSnapshot snapshot
        )
        {
            var result = _contractRegisterer.DecodeContract(address, invocation);
            if (result is null)
                return OperatingError.ContractFailed;
            var (contract, method, args) = result;
            try
            {
                var context = new ContractContext
                {
                    Snapshot = snapshot,
                    Sender = receipt.Transaction.From,
                    Receipt = receipt
                };
                var inst = Activator.CreateInstance(contract, context);
                method.Invoke(inst, args);
                OnSystemContractInvoked?.Invoke(this, context);
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