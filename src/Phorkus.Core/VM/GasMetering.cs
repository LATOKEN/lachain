namespace Phorkus.Core.VM
{
    public class GasMetering
    {
        public const ulong DefaultBlockGasLimit = 100_000_000_000;
        public const ulong DefaultTxTransferGasCost = 3_000_000;
        
        public const ulong CopyFromMemoryGasPerByte = 10;
        public const ulong CopyToMemoryGasPerByte = 10;
        public const ulong GetCallValueGasCost = 100;
        public const ulong GetCallSizeGasCost = 10;
        public const ulong TransferFundsGasCost = 3_000_000;
        public const ulong LoadStorageGasCost = 500_000;
        public const ulong SaveStorageGasCost = 3_000_000;
        public const ulong Keccak256GasCost= 0;
        public const ulong Keccak256GasPerByte = 100_000;
        public const ulong Sha256GasGasCost = 0;
        public const ulong Sha256GasPerByte = 100_000;
        public const ulong Ripemd160GasCost = 0;
        public const ulong Ripemd160GasPerByte = 100_000;
        public const ulong Murmur3GasCost = 0;
        public const ulong Murmur3GasPerByte = 100_000;
        public const ulong RecoverGasCost = 100_000;
        public const ulong VerifyGasCost = 60_000;
        public const ulong WriteEventPerByteGas = SaveStorageGasCost / 32;
    }
}