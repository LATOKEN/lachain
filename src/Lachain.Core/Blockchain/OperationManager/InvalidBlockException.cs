using System;

namespace Lachain.Core.Blockchain.OperationManager
{
    public class InvalidBlockException : Exception
    {
        public OperatingError OperatingError;
        
        public InvalidBlockException(OperatingError operatingError)
        {
            OperatingError = operatingError;
        }
    }
}