using System;

namespace Phorkus.Core.VM
{
    public class HaltException : Exception
    {
        public int HaltCode { get; }

        public HaltException(int haltCode) : base("Frame execution prevented from smart-contract with halt code (" + haltCode + ")")
        {
            HaltCode = haltCode;
        }
    }
}