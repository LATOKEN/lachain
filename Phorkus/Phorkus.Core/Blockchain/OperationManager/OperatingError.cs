namespace Phorkus.Core.Blockchain.OperationManager
{
    public enum OperatingError : byte
    {
        Ok,
        HashMismatched,
        UnsupportedVersion,
        SizeMismatched,
        InvalidNonce,
        UnsupportedTransaction,
        InvalidSignature
    }
}