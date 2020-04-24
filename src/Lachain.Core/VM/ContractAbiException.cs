using System;

namespace Lachain.Core.VM
{
    public class ContractAbiException : Exception
    {
        public ContractAbiException(string message) : base(message)
        {
        }
    }
}