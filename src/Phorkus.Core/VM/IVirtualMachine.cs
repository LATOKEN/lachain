using Phorkus.Proto;

namespace Phorkus.Core.VM
{
    public interface IVirtualMachine
    {
        bool VerifyContract(byte[] code);
        
        InvocationResult InvokeContract(Contract contract, InvocationContext context, byte[] input, ulong gasLimit);
    }
}