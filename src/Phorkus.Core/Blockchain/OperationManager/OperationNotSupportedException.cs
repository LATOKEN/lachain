using System;

namespace Phorkus.Core.Blockchain.OperationManager
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