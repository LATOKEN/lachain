using Lachain.Core.Blockchain.SystemContracts.ContractManager;

namespace Lachain.Core.Blockchain.SystemContracts.Interface
{
    public class GovernanceInterface : IContractInterface
    {
        public const string MethodChangeValidators = "changeValidators(uint256,bytes[])";
        public const string MethodKeygenCommit = "keygenCommit(uint256,bytes,bytes[])";
        public const string MethodKeygenSendValue = "keygenSendValue(uint256,uint256,bytes[])";
        public const string MethodKeygenConfirm = "keygenConfirm(uint256,bytes,bytes[])";
        public const string MethodKeygenConfirmWithVerification = "keygenConfirmWithVerification(uint256,bytes,bytes[],bytes[])";
        public const string MethodFinishCycle = "finishCycle(uint256)";
        public const string MethodIsNextValidator = "isNextValidator(bytes)";
        public const string MethodDistributeCycleRewardsAndPenalties = "distibuteCycleRewardsAndPenalties(uint256)";

        public const string EventChangeValidators = "ChangeValidators(bytes[])";
        public const string EventKeygenCommit = "KeygenCommit(bytes,bytes[])";
        public const string EventKeygenSendValue = "KeygenSendValue(uint256,bytes[])";
        public const string EventKeygenConfirm = "KeygenConfirm(bytes,bytes[])";
        public const string EventKeygenConfirmWithVerificationKeys = "KeygenConfirmWithVerificationKeys(bytes,bytes[])";
        public const string EventFinishCycle = "FinishCycle()";
        public const string EventDistributeCycleRewardsAndPenalties = "DistibuteCycleRewardsAndPenalties(uint256)";


        public string[] Methods { get; } =
        {
            MethodChangeValidators,
            MethodKeygenCommit,
            MethodKeygenSendValue,
            MethodFinishCycle,
            MethodKeygenConfirm,
            MethodKeygenConfirmWithVerification, 
            MethodIsNextValidator
        };

        public string[] Properties { get; } =
        {
        };

        public string[] Events { get; } =
        {
            EventChangeValidators,
            EventKeygenCommit,
            EventKeygenSendValue,
            EventKeygenConfirm,
            EventKeygenConfirmWithVerificationKeys, 
            EventFinishCycle,
            EventDistributeCycleRewardsAndPenalties
        };
    }
}