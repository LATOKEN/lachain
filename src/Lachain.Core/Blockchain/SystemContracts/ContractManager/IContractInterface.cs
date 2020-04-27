namespace Lachain.Core.Blockchain.SystemContracts.ContractManager
{
    public interface IContractInterface
    {
        string[] Methods { get; }

        string[] Properties { get; }
        
        string[] Events { get; }
    }
}