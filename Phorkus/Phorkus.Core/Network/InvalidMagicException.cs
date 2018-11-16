using System;

namespace Phorkus.Core.Network
{
    public class InvalidMagicException : Exception
    {
        public InvalidMagicException()
        {
        }
        
        public InvalidMagicException(string message) : base(message)
        {
        }
    }
}