using System;
using Lachain.Proto;

namespace Lachain.Core.Blockchain.VM.ExecutionFrame
{
    public interface IExecutionFrame : IDisposable
    {
        ExecutionStatus Execute();

        public ulong GasLimit { get; }
        public ulong GasUsed { get; }

        void UseGas(ulong gas);

        public byte[] ReturnValue { get; set; }

        public byte[] LastChildReturnValue { get; set; }

        public UInt256[]? Logs { get; set; }
        public byte[] Input { get; }

        public InvocationContext InvocationContext { get; }
        public UInt160 CurrentAddress { get; }
    }
}