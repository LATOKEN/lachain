using Lachain.Core.Blockchain.VM;
using Lachain.Proto;

namespace Lachain.Core.Blockchain.Interface
{
    public interface IVirtualMachine
    {
        bool VerifyContract(byte[] code);
        
        InvocationResult InvokeContract(Contract contract, InvocationContext context, byte[] input, ulong gasLimit);
    }
}