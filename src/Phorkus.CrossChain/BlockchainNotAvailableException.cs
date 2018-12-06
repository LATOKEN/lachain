using System;

namespace Phorkus.CrossChain
{
    public class BlockchainNotAvailableException : Exception
    {
        public BlockchainNotAvailableException(string message) : base(message)
        {
        }
    }
}