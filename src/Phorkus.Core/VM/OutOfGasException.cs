using System;

namespace Phorkus.Core.VM
{
    public class OutOfGasException : Exception
    {
        public ulong GasUsed { get; }
        public ulong GasLimit { get; }

        public OutOfGasException(ulong gasUsed, ulong gasLimit)
        {
            GasUsed = gasUsed;
            GasLimit = gasLimit;
        }
    }
}