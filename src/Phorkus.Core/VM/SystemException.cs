using System;

namespace Phorkus.Core.VM
{
    public class SystemException : Exception
    {
        public SystemException(string message) : base(message)
        {
        }
    }
}