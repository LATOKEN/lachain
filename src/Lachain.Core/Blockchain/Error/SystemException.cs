using System;

namespace Lachain.Core.Blockchain.Error
{
    public class SystemException : Exception
    {
        public SystemException(string message) : base(message)
        {
        }
    }
}