using System;
using System.Collections.Generic;
using System.Linq;
using Phorkus.Core.Utils;
using Phorkus.Core.VM;
using Phorkus.Proto;
using Phorkus.Storage.State;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.Blockchain.OperationManager
{
    public class DeployTransactionExecuter : ITransactionExecuter
    {
        private readonly IVirtualMachine _virtualMachine;
        
        public DeployTransactionExecuter(IVirtualMachine virtualMachine)
        {
            _virtualMachine = virtualMachine;
        }

        public OperatingError Execute(Block block, TransactionReceipt receipt, IBlockchainSnapshot snapshot)
        {
            var error = _CheckGasLimit(receipt);
            if (error != OperatingError.Ok)
                return error;
            var transaction = receipt.Transaction;
            /* validate transaction before execution */
            error = Verify(transaction);
            if (error != OperatingError.Ok)
                return error;
            /* calculate contract hash and register it */
            var hash = transaction.From.Buffer.ToArray().Concat(BitConverter.GetBytes((uint) transaction.Nonce)).ToHash160();
            var contract = new Contract
            {
                ContractAddress = hash,
                ByteCode = transaction.Deploy
            };
            if (!_virtualMachine.VerifyContract(contract.ByteCode.ToByteArray()))
                return OperatingError.InvalidContract;
            snapshot.Contracts.AddContract(transaction.From, contract);
            /* invoke contract constructor */
            return _InvokeConstructor(contract, receipt, block);
        }
        
        private OperatingError _CheckGasLimit(TransactionReceipt receipt)
        {
            const ulong inputDataGas = 10;
            if (receipt.Transaction.Invocation.IsEmpty)
                return OperatingError.Ok;
            var totalLength = receipt.Transaction.Invocation.Length + receipt.Transaction.Deploy.Length;
            receipt.GasUsed += (ulong) totalLength * inputDataGas;
            return receipt.GasUsed > receipt.Transaction.GasLimit ? OperatingError.OutOfGas : OperatingError.Ok;
        }
        
        private OperatingError _InvokeConstructor(Contract contract, TransactionReceipt receipt, Block block)
        {
            var input = receipt.Transaction.Invocation.ToByteArray();
            if (!_IsConstructorCall(input))
                return OperatingError.InvalidInput;
            try
            {
                var result = _virtualMachine.InvokeContract(contract, new InvocationContext(receipt.Transaction.From, receipt.Transaction, block), input, receipt.Transaction.GasLimit - receipt.GasUsed);
                return result.Status != ExecutionStatus.Ok ? OperatingError.ContractFailed : OperatingError.Ok;
            }
            catch (OutOfGasException e)
            {
                receipt.GasUsed += e.GasUsed;
            }

            return OperatingError.OutOfGas;
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
        
        public OperatingError Verify(Transaction transaction)
        {
            if (transaction.Type != TransactionType.Deploy)
                return OperatingError.InvalidTransaction;
            if (transaction.Deploy is null || transaction.Deploy.IsEmpty)
                return OperatingError.InvalidTransaction;
            if (transaction.Invocation is null || transaction.Invocation.IsEmpty)
                return OperatingError.InvalidTransaction;
            var result = _VerifyContract(transaction);
            return result;
        }

        private OperatingError _VerifyContract(Transaction transaction)
        {
            /* TODO: "validate opcodes here" */
            return OperatingError.Ok;
        }
    }
}