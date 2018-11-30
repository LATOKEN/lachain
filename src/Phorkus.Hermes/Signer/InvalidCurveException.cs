using System;

namespace Phorkus.Hermes.Signer
{
    public class InvalidCurveException : Exception
    {
        public InvalidCurveException(string message) : base(message)
        {
        }
    }
}