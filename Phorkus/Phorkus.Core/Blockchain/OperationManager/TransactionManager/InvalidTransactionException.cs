using System;

namespace Phorkus.Core.Blockchain.OperationManager.TransactionManager
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