using Lachain.Core.Blockchain.SystemContracts.ContractManager;

namespace Lachain.Core.Blockchain.SystemContracts.Interface
{
    public class DeployInterface : IContractInterface
    {
        public const string MethodDeploy = "deploy(bytes)";
        public const string MethodGetDeployHeight = "getDEployGeight(uint256)";
        
        public string[] Methods { get; } =
        {
            MethodDeploy,
            MethodGetDeployHeight
        };

        public string[] Properties { get; } =
        {
        };

        public string[] Events { get; } =
        {
        };
    }
}