using System;
using Phorkus.Core.Blockchain.ContractManager.Standards;

namespace Phorkus.Core.Blockchain.ContractManager
{
    public class StandardFactory : IStandardFactory
    {
        public IContractInterface FactoryFromName(string name)
        {
            if (!Enum.TryParse<ContractStandard>(name, true, out var contractStandard))
                throw new ArgumentOutOfRangeException(nameof(name), name, "Invalid contract standard specified (" + name + ")");
            return FactoryFromType(contractStandard);
        }

        public IContractInterface FactoryFromType(ContractStandard contractStandard)
        {
            switch (contractStandard)
            {
                case ContractStandard.Lrc20:
                    return new Lrc20Interface();
                default:
                    throw new ArgumentOutOfRangeException(nameof(contractStandard), contractStandard, "Invalid contract standard specified (" + contractStandard + ")");
            }
        }
    }
}