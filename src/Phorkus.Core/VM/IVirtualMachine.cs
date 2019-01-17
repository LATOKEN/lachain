using Phorkus.Proto;

namespace Phorkus.Core.VM
{
    public interface IVirtualMachine
    {
        bool VerifyContract(Contract contract);
        
        bool InvokeContract(Contract contract, Invocation invocation);
    }
}