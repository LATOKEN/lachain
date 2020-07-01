namespace Lachain.Core.Blockchain.Error
{
    public class InvalidContractException : System.Exception
    {
        public InvalidContractException()
        {
        }

        public InvalidContractException(string message) : base(message)
        {
        }
    }
}