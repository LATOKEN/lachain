using System;

namespace Lachain.Core.Blockchain.Error
{
    public class ContractAbiException : Exception
    {
        public ContractAbiException(string message) : base(message)
        {
        }
    }
}