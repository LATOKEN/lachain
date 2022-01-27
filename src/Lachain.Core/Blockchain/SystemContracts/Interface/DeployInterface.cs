using Lachain.Core.Blockchain.SystemContracts.ContractManager;

namespace Lachain.Core.Blockchain.SystemContracts.Interface
{
    public class DeployInterface : IContractInterface
    {
        public const string MethodDeploy = "deploy(bytes)";
        public const string MethodGetDeployHeight = "getDeployHeight(address)";
        public const string MethodSetDeployHeight = "setDeployHeight(address, bytes)";
        
        public string[] Methods { get; } =
        {
            MethodDeploy,
            MethodGetDeployHeight,
            MethodSetDeployHeight
        };

        public string[] Properties { get; } =
        {
        };

        public string[] Events { get; } =
        {
        };
    }
}