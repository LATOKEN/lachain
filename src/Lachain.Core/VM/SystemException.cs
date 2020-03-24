using System;

namespace Lachain.Core.VM
{
    public class SystemException : Exception
    {
        public SystemException(string message) : base(message)
        {
        }
    }
}