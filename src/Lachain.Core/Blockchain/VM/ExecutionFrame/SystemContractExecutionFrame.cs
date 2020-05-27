using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Proto;

namespace Lachain.Core.Blockchain.VM.ExecutionFrame
{
    public class SystemContractExecutionFrame : IExecutionFrame
    {
        private readonly SystemContractCall _call;

        public SystemContractExecutionFrame(
            SystemContractCall call, InvocationContext context, byte[] input, ulong gasLimit
        )
        {
            _call = call;
            GasLimit = gasLimit;
            _gasRemaining = gasLimit;
            Input = input;
            InvocationContext = context;
            CurrentAddress = call.GetAddress();
            ReturnValue = new byte[] { };
        }

        public void Dispose()
        {
        }

        public ExecutionStatus Execute()
        {
            return _call.Invoke(this);
        }

        public ulong GasLimit { get; }
        private ulong _gasRemaining;
        public ulong GasUsed => GasLimit - _gasRemaining;

        public void UseGas(ulong gas)
        {
            checked
            {
                _gasRemaining -= gas;
            }
        }

        public byte[] ReturnValue { get; set; }
        public byte[] Input { get; }
        public InvocationContext InvocationContext { get; }
        public UInt160 CurrentAddress { get; }
    }
}