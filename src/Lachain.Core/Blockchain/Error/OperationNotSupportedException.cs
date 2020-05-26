namespace Lachain.Core.Blockchain.Error
{
    public class OperationNotSupportedException : System.Exception
    {
        public OperationNotSupportedException()
        {
        }

        public OperationNotSupportedException(string message) : base(message)
        {
        }
    }
}