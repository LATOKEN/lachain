namespace Lachain.Core.Blockchain.ContractManager.Standards
{
    public class Lrc20Interface : IContractInterface
    {
        public const string MethodTotalSupply = "totalSupply()";
        public const string MethodBalanceOf = "balanceOf(address)";
        public const string MethodTransfer = "transfer(address,uint256)";
        public const string MethodTransferFrom = "transferFrom(address,address,uint256)";
        public const string MethodApprove = "approve(address,uint256)";
        public const string MethodAllowance = "allowance(address,address)";

        public const string PropertyName = "name()";
        public const string PropertyDecimals = "decimals()";
        public const string PropertySymbol = "symbol()";

        public const string EventTransfer = "Transfer(address,address,uint256)";
        public const string EventApproval = "Approval(address,address,uint256)";
        
        public string[] Methods { get; } =
        {
            MethodTotalSupply,
            MethodBalanceOf,
            MethodTransfer,
            MethodTransferFrom,
            MethodApprove,
            MethodAllowance
        };

        public string[] Properties { get; } =
        {
            PropertyName,
            PropertyDecimals,
            PropertySymbol
        };

        public string[] Events { get; } =
        {
            EventTransfer,
            EventApproval
        };
    }
}