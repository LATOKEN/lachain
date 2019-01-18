using System;

namespace Phorkus.Party.Signer
{
    public class InvalidSignatureException : Exception
    {
        public InvalidSignatureException(string message) : base(message)
        {
        }
    }
}