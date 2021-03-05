using System;

namespace Lachain.Core.Blockchain.Error
{
    public class OperationNotSupportedException : Exception
    {
        public OperationNotSupportedException()
        {
        }

        public OperationNotSupportedException(string message) : base(message)
        {
        }
    }
}