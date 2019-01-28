namespace Phorkus.Core.VM
{
    public enum ExecutionStatus
    {
        OK = 0,
        INCOMPATIBLE_CODE = 1,
        COMPILATION_FAILURE = 2,
        MISSING_SYMBOL = 3,
        NO_SUCH_METHOD = 4,
        INCORRECT_CALL = 5,
        UNKNOWN_ERROR = -1,
        VM_CORRUPTION = -2,
    }
}