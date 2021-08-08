using Lachain.Core.Blockchain.SystemContracts.ContractManager;

namespace Lachain.Core.Blockchain.SystemContracts.Interface
{
    public class Lrc20Interface : IContractInterface
    {
        public const string MethodTotalSupply = "totalSupply()";
        public const string MethodBalanceOf = "balanceOf(address)";
        public const string MethodTransfer = "transfer(address,uint256)";
        public const string MethodTransferFrom = "transferFrom(address,address,uint256)";
        public const string MethodApprove = "approve(address,uint256)";
        public const string MethodAllowance = "allowance(address,address)";
        public const string MethodName = "name()";
        public const string MethodDecimals = "decimals()";
        public const string MethodSymbol = "symbol()";
        public const string MethodMint = "mint(address,uint256)";
        public const string MethodSetAllowedSupply = "setAllowedSupply(uint256)";
        public const string MethodGetAllowedSupply = "getAllowedSupply()";

        public const string EventTransfer = "Transfer(address,address,uint256)";
        public const string EventApproval = "Approval(address,address,uint256)";

        public string[] Methods { get; } =
        {
            MethodTotalSupply,
            MethodBalanceOf,
            MethodTransfer,
            MethodTransferFrom,
            MethodApprove,
            MethodAllowance,
            MethodMint,
            MethodSetAllowedSupply,
            MethodGetAllowedSupply
        };

        public string[] Properties { get; } =
        {
            MethodName,
            MethodDecimals,
            MethodSymbol
        };

        public string[] Events { get; } =
        {
            EventTransfer,
            EventApproval
        };
    }
}