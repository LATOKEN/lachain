using System;

namespace Lachain.Core.Blockchain.Error
{
    public class InvalidContractException : Exception
    {
        public InvalidContractException()
        {
        }

        public InvalidContractException(string message) : base(message)
        {
        }
    }
}