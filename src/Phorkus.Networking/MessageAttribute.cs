using System;

namespace Phorkus.Networking
{
    [AttributeUsage(AttributeTargets.Method)]
    public class MessageAttribute : Attribute
    {
        public Type MessageType { get; set; }
    }
}