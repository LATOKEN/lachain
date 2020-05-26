namespace Lachain.Core.Blockchain.Error
{
    public class InvalidTransactionException : System.Exception
    {
        public OperatingError OperatingError;
        
        public InvalidTransactionException(OperatingError operatingError)
        {
            OperatingError = operatingError;
        }
    }
}