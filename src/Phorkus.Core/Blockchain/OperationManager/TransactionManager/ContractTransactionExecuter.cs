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
            /* TODO: "verify contract nonce here" */
            /* validate transaction before execution */
            var error = Verify(transaction);
            if (error != OperatingError.Ok)
                return error;
            var contract = transaction.Contract;
            /* try to transfer funds from sender to recipient */
            if (!contract.Value.IsZero())
                snapshot.Balances.TransferAvailableBalance(transaction.From, contract.To, contract.Asset, new Money(contract.Value));
            /* if we have contract block than register contract */
            if (contract.Contract != null)
                return _RegisterContract(transaction.From, contract.Contract, snapshot);
            /* if we have invocation block than invoke contract method */
            if (contract.Invocation != null)
                return _InvokeContract(contract.Invocation, snapshot);
            return OperatingError.Ok;
        }
        
        private OperatingError _RegisterContract(UInt160 from, Contract contract, IBlockchainSnapshot snapshot)
        {
            if (contract is null)
                return OperatingError.Ok;
            snapshot.Contracts.AddContract(from, contract);
            return OperatingError.Ok;
        }

        private OperatingError _InvokeContract(Invocation invocation, IBlockchainSnapshot snapshot)
        {
            var contract = snapshot.Contracts.GetContractByHash(invocation.ContractAddress);
            if (contract is null)
                return OperatingError.ContractNotFound;
            return _virtualMachine.InvokeContract(contract, invocation) != ExecutionStatus.OK ? OperatingError.ContractFailed : OperatingError.Ok;
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
            if (contract.Contract != null && contract.Invocation != null)
                return OperatingError.InvalidTransaction;
            /* TODO: "validate contract hash here" */
            var result = _VerifyContract(contract.Contract);
            if (result != OperatingError.Ok)
                return result;
            result = _VerifyInvocation(contract.Invocation);
            return result;
        }

        private static OperatingError _VerifyContract(Contract contract)
        {
            if (contract is null)
                return OperatingError.Ok;
            if (contract.Version != ContractVersion.Wasm)
                return OperatingError.InvalidTransaction;
            if (contract.Wasm is null || contract.Wasm.IsEmpty)
                return OperatingError.InvalidTransaction;
            /* TODO: "validate opcodes here" */
            return OperatingError.Ok;
        }

        private static OperatingError _VerifyInvocation(Invocation invocation)
        {
            if (invocation is null)
                return OperatingError.Ok;
            if (string.IsNullOrWhiteSpace(invocation.MethodName))
                return OperatingError.InvalidTransaction;
            if (invocation.ContractAddress is null)
                return OperatingError.InvalidTransaction;
            return OperatingError.Ok;
        }
    }
}