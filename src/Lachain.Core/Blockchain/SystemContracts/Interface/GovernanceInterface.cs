using Lachain.Core.Blockchain.SystemContracts.ContractManager;

namespace Lachain.Core.Blockchain.SystemContracts.Interface
{
    public class GovernanceInterface : IContractInterface
    {
        public const string MethodChangeValidators = "changeValidators(bytes[])";
        public const string MethodKeygenCommit = "keygenCommit(bytes,bytes[])";
        public const string MethodKeygenSendValue = "keygenSendValue(uint256,bytes[])";
        public const string MethodKeygenConfirm = "keygenConfirm(bytes,bytes[])";
        public const string MethodFinishCycle = "finishCycle()";
        public const string MethodDistributeCycleRewardsAndPenalties = "distibuteCycleRewardsAndPenalties()";
        
        public string[] Methods { get; } =
        {
            MethodChangeValidators,
            MethodKeygenCommit,
            MethodKeygenSendValue,
            MethodFinishCycle,
            MethodKeygenConfirm
        };

        public string[] Properties { get; } =
        {
        };

        public string[] Events { get; } =
        {
        };
    }
}