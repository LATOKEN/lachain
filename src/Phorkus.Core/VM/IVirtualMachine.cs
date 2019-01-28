﻿using Phorkus.Proto;

namespace Phorkus.Core.VM
{
    public interface IVirtualMachine
    {
        bool VerifyContract(Contract contract);
        
        ExecutionStatus InvokeContract(Contract contract, UInt160 sender, byte[] input);
    }
}