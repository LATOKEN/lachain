namespace Lachain.Core.Blockchain.VM
{
    public class GasMetering
    {
        public const ulong DefaultBlockGasLimit = 100_000_000_000;
        public const ulong DefaultTxTransferGasCost = 3_000_000;
        public const ulong InputDataGasPerByte = 10;
        
        public const ulong CopyFromMemoryGasPerByte = 10;
        public const ulong CopyToMemoryGasPerByte = 10;
        public const ulong GetCallValueGasCost = 100;
        public const ulong GetCallSizeGasCost = 10;
        public const ulong TransferFundsGasCost = 3_000_000;
        public const ulong LoadStorageGasCost = 500_000;
        public const ulong SaveStorageGasCost = 3_000_000;
        public const ulong KillStorageGasCost = 3_000_000;
        public const ulong Keccak256GasCost= 0;
        public const ulong Keccak256GasPerByte = 100_000;
        public const ulong Sha256GasGasCost = 0;
        public const ulong Sha256GasPerByte = 100_000;
        public const ulong Ripemd160GasCost = 0;
        public const ulong Ripemd160GasPerByte = 100_000;
        public const ulong RecoverGasCost = 100_000;
        public const ulong VerifyGasCost = 60_000;
        public const ulong WriteEventPerByteGas = SaveStorageGasCost / 32;
        public const ulong CallDataLoad = 1_000;
        public const ulong MLoad = 1_000;
        public const ulong Number = 1_000;

        public const ulong ChangeValidatorsCost = 1_000_000;
        public const ulong KeygenCommitCost = 1_000_000;
        public const ulong KeygenSendValueCost = 1_000_000;
        public const ulong KeygenConfirmCost = 1_000_000;
        public const ulong GovernanceIsNextValidatorCost = 500_000;
        
        public const ulong DeployCost = 1_000_000;
        public const ulong DeployCostPerByte = 1_000;
        
        public const ulong NativeTokenNameCost = 1_000;
        public const ulong NativeTokenDecimalsCost = 1_000;
        public const ulong NativeTokenSymbolCost = 1_000;
        public const ulong NativeTokenTotalSupplyCost = 1_000;
        public const ulong NativeTokenBalanceOfCost = 1_000;
        public const ulong NativeTokenTransferCost = 21_000;
        public const ulong NativeTokenTransferFromCost = 21_000;
        public const ulong NativeTokenApproveCost = 21_000;
        public const ulong NativeTokenAllowanceCost = 21_000;
        
        
        public const ulong StakingBecomeStakerCost = 6_000_000;
        public const ulong StakingRequestStakeWithdrawalCost = 6_000_000;
        public const ulong StakingWithdrawStakeCost = 6_000_000;
        public const ulong StakingSubmitVrfCost = 6_000_000;
        public const ulong StakingSubmitAttendanceDetectionCost = 12_000_000;
        public const ulong StakingGetStakeCost = 500_000;
        public const ulong StakingGetPenaltyCost = 500_000;
        public const ulong StakingGetPreviousValidators = 500_000;
        public const ulong StakingGetWithdrawRequestCycleCost = 500_000;
        public const ulong StakingGetStartCycleCost = 500_000;
        public const ulong StakingIsPreviousValidatorCost = 500_000;
        public const ulong StakingIsCheckedInAttendanceDetectionCost = 500_000;
        public const ulong StakingGetVrfSeedCost = 500_000;
        public const ulong StakingIsAbleToBeAValidatorCost = 500_000;
        public const ulong StakingGetTotalActiveStakeCost = 500_000;
        
        // Math
        public const ulong EQ = 3_000;
        public const ulong LT = 3_000;
        public const ulong GT = 3_000;
        public const ulong IsZero = 3_000;
        public const ulong Add = 3_000;
        public const ulong Sub = 3_000;
    }
}