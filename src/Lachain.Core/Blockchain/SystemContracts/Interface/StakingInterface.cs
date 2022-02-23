using Lachain.Core.Blockchain.SystemContracts.ContractManager;

namespace Lachain.Core.Blockchain.SystemContracts.Interface
{
    public class StakingInterface : IContractInterface
    {
        public const string MethodBecomeStaker = "becomeStaker(bytes,uint256)";
        public const string MethodRequestStakeWithdrawal = "requestStakeWithdrawal(bytes)";
        public const string MethodWithdrawStake = "withdrawStake(bytes)";
        public const string MethodSubmitVrf = "submitVrf(bytes,bytes)";
        public const string MethodSubmitAttendanceDetection = "submitAttendanceDetection(bytes[],uint[])";
        public const string MethodFinishVrfLottery = "finishVrfLottery()";
        public const string MethodGetTotalActiveStake = "totalActiveStake()";
        public const string MethodGetStake = "getStake(address)";
        public const string MethodGetPenalty = "getPenalty(address)";
        public const string MethodGetWithdrawRequestCycle = "getWithdrawRequestCycle(address)";
        public const string MethodGetStartCycle = "getStartCycle(address)";
        public const string MethodIsAbleToBeValidator = "isAbleToBeValidator(address)";
        public const string MethodIsNextValidator = "isNextValidator(bytes)";
        public const string MethodGetVrfSeed = "getVrfSeed()";
        public const string MethodGetNextVrfSeed = "getNextVrfSeed()";
        public const string MethodGetPreviousValidators = "getPreviousValidators()";
        public const string MethodIsPreviousValidator = "isPreviousValidator(bytes)";
        public const string MethodIsCheckedInAttendanceDetection = "isCheckedInAttendanceDetection(bytes)";
        public const string MethodGetStakerTotalStake = "getStakerTotalStake(address)";
        
        public string[] Methods { get; } =
        {
            MethodBecomeStaker,
            MethodRequestStakeWithdrawal,
            MethodWithdrawStake,
            MethodSubmitVrf,
            MethodFinishVrfLottery,
            MethodGetTotalActiveStake,
            MethodGetStake,
            MethodGetPenalty,
            MethodGetWithdrawRequestCycle,
            MethodIsAbleToBeValidator,
            MethodIsNextValidator,
            MethodGetVrfSeed,
            MethodSubmitAttendanceDetection,
            MethodGetPreviousValidators,
            MethodIsPreviousValidator,
            MethodGetStakerTotalStake
        };

        public string[] Properties { get; } =
        {
        };

        public string[] Events { get; } =
        {
        };
    }
}