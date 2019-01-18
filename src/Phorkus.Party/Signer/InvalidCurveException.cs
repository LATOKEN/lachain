using System;

namespace Phorkus.Party.Signer
{
    public class InvalidCurveException : Exception
    {
        public InvalidCurveException(string message) : base(message)
        {
        }
    }
}