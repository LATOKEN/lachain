using System;

namespace Lachain.Core.Blockchain.OperationManager
{
    public class InvalidTransactionException : Exception
    {
        public OperatingError OperatingError;
        
        public InvalidTransactionException(OperatingError operatingError)
        {
            OperatingError = operatingError;
        }
    }
}