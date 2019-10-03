namespace Phorkus.Core.VM
{
    public enum ExecutionStatus
    {
        Ok = 0,
        CompilationFailure = 1,
        MissingEntry = 2,
        ContractNotFound = 3,
        ExecutionHalted = 4,
        GasOverflow = 5,
        UnknownError = -1,
        VmStackCorruption = -2,
        JitCorruption = -3
    }
}