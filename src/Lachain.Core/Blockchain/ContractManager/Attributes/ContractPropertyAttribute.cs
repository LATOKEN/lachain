using System;

namespace Lachain.Core.Blockchain.ContractManager.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ContractPropertyAttribute : Attribute
    {
        public string Property { get; }
        
        public ContractPropertyAttribute(string property)
        {
            Property = property;
        }
    }
}