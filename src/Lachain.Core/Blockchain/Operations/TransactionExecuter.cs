using System;
using System.Linq;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.VM;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Utility;
using Lachain.Utility.Utils;

namespace Lachain.Core.Blockchain.Operations
{
    public class TransactionExecuter
    {
        private readonly IContractRegisterer _contractRegisterer;
        public event EventHandler<InvocationContext>? OnSystemContractInvoked;

        public TransactionExecuter(IContractRegisterer contractRegisterer)
        {
            _contractRegisterer = contractRegisterer;
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
                snapshot.Balances.AddBalance(receipt.Transaction.To, transaction.Value.ToMoney(false), true);
                return OperatingError.Ok;
            }

            if (receipt.Transaction.To.Buffer.IsEmpty) // this is deploy transaction
            {
                var invocation = ContractEncoder.Encode("deploy(bytes)", transaction.Invocation.ToArray());
                return _InvokeContract(ContractRegisterer.DeployContract, invocation, receipt, snapshot, true);
            }

            var contract = snapshot.Contracts.GetContractByHash(transaction.To);
            var systemContract = _contractRegisterer.GetContractByAddress(transaction.To);
            if (contract is null && systemContract is null)
            {
                /*
                 * Destination address is not smart-contract, just plain address
                 * So we just call transfer method of system contract
                 */
                if (snapshot.Balances.GetBalance(transaction.From) < transaction.Value.ToMoney())
                    return OperatingError.InsufficientBalance;
                var invocation = ContractEncoder.Encode("transfer(address,uint256)", transaction.To, transaction.Value);
                return _InvokeContract(ContractRegisterer.LatokenContract, invocation, receipt, snapshot, true);
            }

            /* try to transfer funds from sender to recipient */
            if (new Money(transaction.Value) > Money.Zero)
                if (!snapshot.Balances.TransferBalance(transaction.From, transaction.To, new Money(transaction.Value)))
                    return OperatingError.InsufficientBalance;
            /* invoke required function or fallback */
            return _InvokeContract(
                receipt.Transaction.To, receipt.Transaction.Invocation.ToArray(),
                receipt, snapshot, !(systemContract is null)
            );
        }

        private OperatingError _InvokeContract(
            UInt160 addressTo, byte[] input, TransactionReceipt receipt,
            IBlockchainSnapshot snapshot, bool isSystemContract
        )
        {
            var transaction = receipt.Transaction;
            var context = new InvocationContext(receipt.Transaction.From, snapshot, receipt);
            try
            {
                if (receipt.GasUsed > transaction.GasLimit)
                    return OperatingError.OutOfGas;
                var result = ContractInvoker.Invoke(addressTo, context, input, transaction.GasLimit - receipt.GasUsed);
                receipt.GasUsed += result.GasUsed;
                if (result.Status != ExecutionStatus.Ok)
                    return OperatingError.ContractFailed;

                if (receipt.GasUsed > transaction.GasLimit) return OperatingError.OutOfGas;
                if (isSystemContract) OnSystemContractInvoked?.Invoke(this, context);
                return OperatingError.Ok;
            }
            catch (OutOfGasException e)
            {
                receipt.GasUsed += e.GasUsed;
            }

            return OperatingError.OutOfGas;
        }

        private OperatingError _CheckGasLimit(TransactionReceipt receipt)
        {
            if (receipt.Transaction.Invocation.IsEmpty)
                return OperatingError.Ok;
            receipt.GasUsed += (ulong) receipt.Transaction.Invocation.Length * GasMetering.InputDataGasPerByte;
            return receipt.GasUsed > receipt.Transaction.GasLimit ? OperatingError.OutOfGas : OperatingError.Ok;
        }

        public OperatingError Verify(Transaction transaction)
        {
            return _VerifyInvocation(transaction);
        }

        private OperatingError _VerifyInvocation(Transaction transaction)
        {
            /* TODO: "verify invocation input here" */
            return OperatingError.Ok;
        }
    }
}