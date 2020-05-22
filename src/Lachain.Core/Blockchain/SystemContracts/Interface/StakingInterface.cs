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
        public const string MethodTotalActiveStake = "totalActiveStake()";
        public const string MethodGetStake = "getStake(address)";
        public const string MethodIsAbleToBeAValidator = "isAbleToBeAValidator(address)";
        public const string MethodGetCurrentCycle = "getCurrentCycle()";
        public const string MethodIsNextValidator = "isNextValidator(bytes)";
        public const string MethodGetVrfSeed = "getVrfSeed()";
        public const string MethodGetNextVrfSeed = "getNextVrfSeed()";
        public const string MethodGetPreviousValidators = "getPreviousValidators()";
        
        public string[] Methods { get; } =
        {
            MethodBecomeStaker,
            MethodRequestStakeWithdrawal,
            MethodWithdrawStake,
            MethodSubmitVrf,
            MethodFinishVrfLottery,
            MethodTotalActiveStake,
            MethodGetStake,
            MethodIsAbleToBeAValidator,
            MethodGetCurrentCycle,
            MethodIsNextValidator,
            MethodGetVrfSeed,
            MethodSubmitAttendanceDetection,
            MethodGetPreviousValidators,
        };

        public string[] Properties { get; } =
        {
        };

        public string[] Events { get; } =
        {
        };
    }
}