using Lachain.Core.Blockchain.SystemContracts.ContractManager;

namespace Lachain.Core.Blockchain.SystemContracts.Interface
{
    public class PassiveStakingInterface : IContractInterface
    {
        public const string MethodPack = "pack(uint256)";
        public const string MethodRedeem = "redeem(uint256)";
        public const string MethodGetRate = "getRate()";
        
        public string[] Methods { get; } =
        {
            MethodPack,
            MethodRedeem,
            MethodGetRate,
        };

        public string[] Properties { get; } =
        {
        };

        public string[] Events { get; } =
        {
        };
    }
}