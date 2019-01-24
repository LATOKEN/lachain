namespace Phorkus.Core.VM
{
    public enum ExecutionStatus
    {
        OK = 0,
        INCOMPATIBLE_CODE = 1,
        COMPILATION_FAILURE = 2,
        MISSING_SYMBOL = 3,
        UNKNOWN_ERROR = -1,
        VM_CORRUPTION = -2,
    }
}