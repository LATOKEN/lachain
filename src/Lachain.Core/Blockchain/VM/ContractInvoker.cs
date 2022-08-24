using Lachain.Core.Blockchain.Interface;
using Lachain.Proto;
using Prometheus;

namespace Lachain.Core.Blockchain.VM
{
    public class ContractInvoker : IContractInvoker
    {
        private static readonly Gauge SystemContractFail = Metrics.CreateGauge(
            "lachain_latest_block_system_contract_call_fail",
            "Index of latest block where system contract call failed",
            new GaugeConfiguration
            {
                LabelNames = new[] {"contract", "method"}
            }
        );
        // TODO: this is hack
        private static IContractRegisterer _contractRegisterer = null!;
        
        public ContractInvoker(IContractRegisterer contractRegisterer)
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
            if (call is null)
                return InvocationResult.WithStatus(ExecutionStatus.ExecutionHalted);
            var result = VirtualMachine.InvokeSystemContract(call, context, input, gasLimit);
            if (result.Status != ExecutionStatus.Ok)
            {
                var contract = _contractRegisterer.GetContractByAddress(address);
                var method = _contractRegisterer.GetMethodName(address, input);
                SystemContractFail.WithLabels(contract!.ToString(), method!).Set(context.Receipt.Block);
            }
            return result;
        }
    }
}