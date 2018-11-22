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
        InvalidSignature,
        DoubleSpend,
        InvalidTransaction,
        AlreadyExists,
        SequenceMismatched,
        InvalidBlock,
        QuorumNotReached,
        InvalidState,
        TransactionLost,
        InvalidMultisig,
        AssetCannotBeIssued,
        AssetNotFound,
        InvalidOwner,
        BlockAlreadyExists,
        AlreadySigned,
    }
}