using Lachain.Proto;

namespace Lachain.Core.Blockchain.VM
{
    public class InvocationResult
    {
        public static InvocationResult WithStatus(ExecutionStatus status)
        {
            return new InvocationResult
            {
                Status = status
            };
        }
        
        public ulong GasUsed { get; internal set; }
        
        public ExecutionStatus Status { get; internal set; }

        public byte[]? ReturnValue { get; internal set; }
    }
}