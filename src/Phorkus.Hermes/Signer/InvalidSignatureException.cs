using System;

namespace Phorkus.Hermes.Signer
{
    public class InvalidSignatureException : Exception
    {
        public InvalidSignatureException(string message) : base(message)
        {
        }
    }
}