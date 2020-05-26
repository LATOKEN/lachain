using System;

namespace Lachain.Core.Blockchain.Error
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