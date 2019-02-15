using System;

namespace Phorkus.Core.Blockchain.ContractManager.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ContractMethodAttribute : Attribute
    {
        public string Method { get; }
        
        public ContractMethodAttribute(string method)
        {
            Method = method;
        }
    }
}