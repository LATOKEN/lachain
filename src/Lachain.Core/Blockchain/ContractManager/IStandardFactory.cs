namespace Lachain.Core.Blockchain.ContractManager
{
    public interface IStandardFactory
    {
        IContractInterface FactoryFromName(string name);

        IContractInterface FactoryFromType(ContractStandard contractStandard);
    }
}