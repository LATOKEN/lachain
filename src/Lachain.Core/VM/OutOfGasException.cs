using System;

namespace Lachain.Core.VM
{
    public class OutOfGasException : Exception
    {
        public ulong GasUsed { get; }

        public OutOfGasException(ulong gasUsed)
        {
            GasUsed = gasUsed;
        }
    }
}