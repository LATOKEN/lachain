namespace Lachain.Core.Blockchain.Error
{
    public class InvalidBlockException : System.Exception
    {
        public OperatingError OperatingError;
        
        public InvalidBlockException(OperatingError operatingError)
        {
            OperatingError = operatingError;
        }
    }
}