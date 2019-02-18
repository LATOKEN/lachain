using Phorkus.Proto;

namespace Phorkus.Core.VM
{
    public interface IVirtualMachine
    {
        bool VerifyContract(byte[] code);
        
        ExecutionStatus InvokeContract(Contract contract, InvocationContext context, byte[] input);
        
        ExecutionStatus InvokeContract(Contract contract, InvocationContext context, byte[] input, out byte[] returnValue);
    }
}