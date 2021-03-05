using Lachain.Core.Blockchain.Interface;
using Lachain.Proto;
using SimpleInjector.Advanced;

namespace Lachain.Core.Blockchain.VM
{
    public class ContractInvoker : IContractInvoker
    {
        // TODO: this is hack
        private static IContractRegisterer? _contractRegisterer = null;

        public static void Init(IContractRegisterer contractRegisterer)
        {
            _contractRegisterer = contractRegisterer;
        }

        public static InvocationResult Invoke(UInt160 address, InvocationContext context, byte[] input, ulong gasLimit)
        {
            var systemContract = _contractRegisterer.GetContractByAddress(address);
            if (systemContract != null)
                return _InvokeSystemContract(context, address, input, gasLimit);

            var contract = context.Snapshot.Contracts.GetContractByHash(address);
            return contract is null
                ? InvocationResult.WithStatus(ExecutionStatus.ContractNotFound)
                : VirtualMachine.InvokeWasmContract(contract, context, input, gasLimit);
        }

        private static InvocationResult _InvokeSystemContract(
            InvocationContext context, UInt160 address, byte[] input, ulong gasLimit
        )
        {
            var call = _contractRegisterer.DecodeContract(context, address, input);
            return call is null
                ? InvocationResult.WithStatus(ExecutionStatus.ExecutionHalted)
                : VirtualMachine.InvokeSystemContract(call, context, input, gasLimit);
        }
    }
}