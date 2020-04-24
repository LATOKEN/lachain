namespace Lachain.Core.Blockchain.ContractManager.Standards
{
    public class DeployInterface : IContractInterface
    {
        public const string MethodDeploy = "deploy(bytes)";
        
        public string[] Methods { get; } =
        {
            MethodDeploy,
        };

        public string[] Properties { get; } =
        {
        };

        public string[] Events { get; } =
        {
        };
    }
}