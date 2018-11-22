using System;

namespace NeoSharp.Core.Exceptions
{
    public class InvalidSignatureException : Exception
    {
        public InvalidSignatureException(string message) : base(message)
        {
        }
    }
}