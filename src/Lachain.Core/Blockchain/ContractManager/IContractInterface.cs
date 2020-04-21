namespace Lachain.Core.Blockchain.ContractManager
{
    public interface IContractInterface
    {
        string[] Methods { get; }

        string[] Properties { get; }
        
        string[] Events { get; }
    }
}