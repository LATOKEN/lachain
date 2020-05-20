using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.VM;
using Lachain.Proto;

namespace Lachain.Core.Blockchain.Interface
{
    public interface IVirtualMachine
    {
        bool VerifyContract(byte[] code);

        InvocationResult InvokeContract(Contract contract, InvocationContext context, byte[] input, ulong gasLimit);

        InvocationResult InvokeSystemContract(
            SystemContractCall systemContractCall, InvocationContext context, byte[] input, ulong gasLimit
        );
    }
}