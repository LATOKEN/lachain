using Lachain.Core.Blockchain.SystemContracts.ContractManager;

namespace Lachain.Core.Blockchain.SystemContracts.Interface
{
    public class StakingInterface : IContractInterface
    {
        public const string MethodBecomeStaker = "becomeStaker(bytes,uint256)";
        public const string MethodRequestStakeWithdrawal = "requestStakeWithdrawal(bytes)";
        public const string MethodWithdrawStake = "withdrawStake(bytes)";
        public const string MethodSubmitVrf = "submitVrf(bytes,bytes,bytes)";
        public const string MethodFinishCycle = "finishCycle()";
        public const string MethodTotalActiveStake = "totalActiveStake()";
        public const string MethodGetStake = "getStake(address)";
        public const string MethodIsAbleToBeAValidator = "isAbleToBeAValidator(address)";
        public const string MethodGetCurrentCycle = "getCurrentCycle()";
        public const string MethodIsNextValidator = "isNextValidator(bytes)";
        public const string MethodGetVrfSeed = "getVrfSeed()";
        
        public string[] Methods { get; } =
        {
            MethodBecomeStaker,
            MethodRequestStakeWithdrawal,
            MethodWithdrawStake,
            MethodSubmitVrf,
            MethodFinishCycle,
            MethodTotalActiveStake,
            MethodGetStake,
            MethodIsAbleToBeAValidator,
            MethodGetCurrentCycle,
            MethodIsNextValidator,
            MethodGetVrfSeed,
        };

        public string[] Properties { get; } =
        {
        };

        public string[] Events { get; } =
        {
        };
    }
}