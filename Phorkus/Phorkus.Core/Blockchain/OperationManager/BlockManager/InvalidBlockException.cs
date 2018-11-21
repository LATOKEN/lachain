using System;

namespace Phorkus.Core.Blockchain.OperationManager.BlockManager
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