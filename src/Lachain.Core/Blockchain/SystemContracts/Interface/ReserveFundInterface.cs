using Lachain.Core.Blockchain.SystemContracts.ContractManager;

namespace Lachain.Core.Blockchain.SystemContracts.Interface
{
    public class ReserveFundInterface : IContractInterface
    {
        public const string MethodPack = "pack(uint256)";
        public const string MethodRedeem = "redeem(uint256)";
        
        public string[] Methods { get; } =
        {
            MethodPack,
            MethodRedeem,
        };

        public string[] Properties { get; } =
        {
        };

        public string[] Events { get; } =
        {
        };
    }
}