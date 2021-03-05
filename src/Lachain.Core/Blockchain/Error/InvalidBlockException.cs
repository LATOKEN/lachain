using System;

namespace Lachain.Core.Blockchain.Error
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