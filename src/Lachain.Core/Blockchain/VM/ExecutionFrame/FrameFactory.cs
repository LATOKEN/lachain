using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Proto;

namespace Lachain.Core.Blockchain.VM.ExecutionFrame
{
    public class FrameFactory
    {
        public static ExecutionStatus FromInvocation(
            byte[] code,
            InvocationContext context,
            UInt160 contract,
            byte[] input,
            IBlockchainInterface blockchainInterface,
            out WasmExecutionFrame frame,
            ulong gasLimit)
        {
            frame = new WasmExecutionFrame(
                WasmExecutionFrame.CompileWasm(contract, code, blockchainInterface.GetFunctionImports(),
                    Hardfork.HardforkHeights.IsHardfork_2Active(context.Snapshot.Blocks.GetTotalBlockHeight())),
                context, contract, input, gasLimit
            );
            return ExecutionStatus.Ok;
        }

        public static ExecutionStatus FromInternalCall(
            byte[] code,
            InvocationContext context,
            UInt160 currentAddress,
            byte[] input,
            IBlockchainInterface blockchainInterface,
            out WasmExecutionFrame frame,
            ulong gasLimit)
        {
            frame = new WasmExecutionFrame(
                WasmExecutionFrame.CompileWasm(currentAddress, code, blockchainInterface.GetFunctionImports(), 
                    Hardfork.HardforkHeights.IsHardfork_2Active(context.Snapshot.Blocks.GetTotalBlockHeight())),
                context, currentAddress, input, gasLimit
            );
            return ExecutionStatus.Ok;
        }

        public static ExecutionStatus FromSystemContractCall(
            SystemContractCall systemContractCall,
            InvocationContext context,
            byte[] input,
            out SystemContractExecutionFrame frame,
            ulong gasLimit
        )
        {
            frame = new SystemContractExecutionFrame(systemContractCall, context, input, gasLimit);
            return ExecutionStatus.Ok;
        }
    }
}