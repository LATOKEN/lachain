namespace Phorkus.Core.VM
{
    public enum ExecutionStatus
    {
        Ok = 0,
        IncompatibleCode = 1,
        CompilationFailure = 2,
        MissingEntrypoint = 3,
        NoSuchContract = 4,
        UnknownError = -1,
        VmCorruption = -2,
    }
}