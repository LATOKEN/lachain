using Lachain.Proto;

namespace Lachain.Core.VM
{
    public interface IVirtualMachine
    {
        bool VerifyContract(byte[] code);
        
        InvocationResult InvokeContract(Contract contract, InvocationContext context, byte[] input, ulong gasLimit);
    }
}